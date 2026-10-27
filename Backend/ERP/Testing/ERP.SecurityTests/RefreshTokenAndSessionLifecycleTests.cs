using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Application.DTOs;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class RefreshTokenAndSessionLifecycleTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RefreshTokenAndSessionLifecycleTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private SecurityDbContext GetSecurityDbContext()
    {
        var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
    }

    #region 1. Login & Token Hash Persistence Tests

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccessTokenAndSetsRefreshToken()
    {
        var client = _fixture.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123",
            DeviceName = "TestDevice"
        });

        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);

        Assert.NotNull(loginData);
        Assert.False(string.IsNullOrWhiteSpace(loginData.Token), "Access token should be returned.");
        Assert.False(string.IsNullOrWhiteSpace(loginData.RefreshToken), "Refresh token should be returned.");
        Assert.NotNull(loginData.SessionId);
        Assert.NotNull(loginData.AccessTokenExpiresAt);

        // Verify HttpOnly cookie was set
        Assert.True(loginRes.Headers.Contains("Set-Cookie"), "Set-Cookie header should be present.");
        var cookies = loginRes.Headers.GetValues("Set-Cookie").ToList();
        var refreshCookie = cookies.FirstOrDefault(c => c.Contains("apex_erp_refresh_token="));
        Assert.NotNull(refreshCookie);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);

        // Verify only the hash is persisted in the database, NEVER the raw token
        using var db = GetSecurityDbContext();
        var session = await db.UserSessions
            .Include(s => s.RefreshTokens)
            .FirstOrDefaultAsync(s => s.Id == loginData.SessionId);

        Assert.NotNull(session);
        Assert.Single(session.RefreshTokens);

        var tokenRecord = session.RefreshTokens.First();
        Assert.NotEqual(loginData.RefreshToken, tokenRecord.TokenHash);

        // The hash in DB must match the SHA256 of the raw token
        byte[] expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(loginData.RefreshToken));
        string expectedHex = Convert.ToHexString(expectedHashBytes);
        Assert.Equal(expectedHex, tokenRecord.TokenHash, ignoreCase: true);
    }

    #endregion

    #region 2. Refresh Token Rotation Tests

    [Fact]
    public async Task Refresh_ValidToken_RotatesTokenAndIssuesNewJwt()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        var firstRefreshToken = loginData.RefreshToken!;

        // 2. Refresh using the refresh token
        var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
        {
            RefreshToken = firstRefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshData = await refreshRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);

        string newAccessToken = refreshData.GetProperty("accessToken").GetString()!;
        string newRefreshToken = refreshData.GetProperty("refreshToken").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(newAccessToken));
        Assert.False(string.IsNullOrWhiteSpace(newRefreshToken));
        Assert.NotEqual(firstRefreshToken, newRefreshToken);

        // 3. Verify in database: first token is marked consumed, successor created in same family
        using var db = GetSecurityDbContext();
        byte[] firstHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(firstRefreshToken));
        string firstHash = Convert.ToHexString(firstHashBytes);

        var consumedRecord = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == firstHash);
        Assert.NotNull(consumedRecord);
        Assert.True(consumedRecord.IsConsumed);
        Assert.NotNull(consumedRecord.ConsumedAt);

        byte[] newHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(newRefreshToken));
        string newHash = Convert.ToHexString(newHashBytes);

        var activeRecord = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == newHash);
        Assert.NotNull(activeRecord);
        Assert.False(activeRecord.IsConsumed);
        Assert.False(activeRecord.IsRevoked);
        Assert.Equal(consumedRecord.TokenFamilyId, activeRecord.TokenFamilyId);
        Assert.Equal(consumedRecord.SequenceNumber + 1, activeRecord.SequenceNumber);
        Assert.Equal(newHash, consumedRecord.ReplacedByTokenHash, ignoreCase: true);
    }

    #endregion

    #region 3. Replay Attack & Token-Family Invalidation Tests

    [Fact]
    public async Task Refresh_ReplayOfConsumedToken_RevokesEntireFamilyAndBlocksSubsequentRefreshes()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);
        var token1 = loginData.RefreshToken!;

        // 2. Legitimate Refresh -> token1 consumed, token2 issued
        var refresh1 = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = token1 });
        Assert.Equal(HttpStatusCode.OK, refresh1.StatusCode);
        var refreshData1 = await refresh1.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var token2 = refreshData1.GetProperty("refreshToken").GetString()!;

        // 3. Attacker replays consumed token1!
        var replayRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = token1 });
        Assert.Equal(HttpStatusCode.Unauthorized, replayRes.StatusCode);

        // 4. Verify DB: entire family and session are marked revoked
        using var db = GetSecurityDbContext();
        byte[] token2HashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token2));
        string token2Hash = Convert.ToHexString(token2HashBytes);

        var token2Record = await db.RefreshTokens
            .Include(rt => rt.UserSession)
            .FirstOrDefaultAsync(rt => rt.TokenHash == token2Hash);

        Assert.NotNull(token2Record);
        Assert.True(token2Record.IsRevoked, "Active token in compromised family must be revoked.");
        Assert.Equal("ReplayBreachDetected", token2Record.RevokedReason);
        Assert.True(token2Record.UserSession.IsRevoked, "Session must be revoked.");
        Assert.Equal("ReplayBreachDetected", token2Record.UserSession.RevokedReason);

        // 5. Subsequent refresh attempt with token2 MUST fail
        var subsequentRefresh = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = token2 });
        Assert.Equal(HttpStatusCode.Unauthorized, subsequentRefresh.StatusCode);
    }

    #endregion

    #region 4. Expiry & Inactive User Tests

    [Fact]
    public async Task Refresh_ExpiredToken_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();

        // Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        // Expire the token in DB
        using (var db = GetSecurityDbContext())
        {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(loginData.RefreshToken!));
            string hash = Convert.ToHexString(hashBytes);
            var tokenRecord = await db.RefreshTokens.FirstAsync(rt => rt.TokenHash == hash);
            tokenRecord.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Attempt refresh
        var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task Refresh_InactiveUser_ReturnsUnauthorizedAndRevokesSession()
    {
        var client = _fixture.CreateClient();

        // 1. Login with a tenant user
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "user_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        // 2. Deactivate the user in database
        using (var db = GetSecurityDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Id == loginData.UserId);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        try
        {
            // 3. Attempt refresh with deactivated user
            var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
            {
                RefreshToken = loginData.RefreshToken
            });

            Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);

            // 4. Verify session is revoked
            using (var db = GetSecurityDbContext())
            {
                var session = await db.UserSessions.FirstAsync(s => s.Id == loginData.SessionId);
                Assert.True(session.IsRevoked);
                Assert.Equal("UserDeactivated", session.RevokedReason);
            }
        }
        finally
        {
            // Re-activate user for remaining tests
            using var db = GetSecurityDbContext();
            var user = await db.Users.FirstAsync(u => u.Id == loginData.UserId);
            user.IsActive = true;
            await db.SaveChangesAsync();
        }
    }

    #endregion

    #region 5. Logout & Session Management Tests

    [Fact]
    public async Task Logout_RevokesSpecificSession_AndIsSafeToRepeat()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        // 2. Logout
        var logoutRes = await client.PostAsJsonAsync("/api/Security/logout", new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        // 3. Repeat logout (must be idempotent/safe to repeat)
        var repeatLogout = await client.PostAsJsonAsync("/api/Security/logout", new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, repeatLogout.StatusCode);

        // 4. Verify session is revoked in DB
        using (var db = GetSecurityDbContext())
        {
            var session = await db.UserSessions.FirstAsync(s => s.Id == loginData.SessionId);
            Assert.True(session.IsRevoked);
            Assert.Equal("UserLogout", session.RevokedReason);
        }

        // 5. Subsequent refresh must fail
        var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesAllActiveSessionsForUser()
    {
        var client = _fixture.CreateClient();

        // 1. Create two separate sessions by logging in twice
        var login1 = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123",
            DeviceName = "Laptop"
        });
        var data1 = await login1.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(data1);

        var login2 = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123",
            DeviceName = "Mobile"
        });
        var data2 = await login2.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(data2);

        // 2. Authenticate as the user and call logout-all
        var authClient = _fixture.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data1.Token);

        var logoutAllRes = await authClient.PostAsync("/api/Security/logout-all", null);
        Assert.Equal(HttpStatusCode.OK, logoutAllRes.StatusCode);

        // 3. Verify both sessions are revoked in DB
        using (var db = GetSecurityDbContext())
        {
            var s1 = await db.UserSessions.FirstAsync(s => s.Id == data1.SessionId);
            var s2 = await db.UserSessions.FirstAsync(s => s.Id == data2.SessionId);

            Assert.True(s1.IsRevoked);
            Assert.True(s2.IsRevoked);
        }

        // 4. Neither refresh token can now be refreshed
        var refresh1 = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = data1.RefreshToken });
        var refresh2 = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = data2.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh2.StatusCode);
    }

    [Fact]
    public async Task GetSessions_And_RevokeSession_EnforcesUserOwnership()
    {
        var client = _fixture.CreateClient();

        // 1. User A logs in
        var loginA = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123",
            DeviceName = "AdminLaptop"
        });
        var dataA = await loginA.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(dataA);

        // 2. User B logs in
        var loginB = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_b@tenant.local",
            Password = "Password@123",
            DeviceName = "TenantBLaptop"
        });
        var dataB = await loginB.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(dataB);

        // 3. User A views active sessions
        var clientA = _fixture.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dataA.Token);

        var getSessionsRes = await clientA.GetAsync("/api/Security/sessions");
        Assert.Equal(HttpStatusCode.OK, getSessionsRes.StatusCode);

        var sessionsList = await getSessionsRes.ReadApiResponseDataAsync<List<SessionDto>>(_jsonOpts);
        Assert.NotNull(sessionsList);
        Assert.Contains(sessionsList, s => s.SessionId == dataA.SessionId);
        Assert.DoesNotContain(sessionsList, s => s.SessionId == dataB.SessionId);

        // 4. User A attempts to revoke User B's session -> must be Forbidden
        var unauthorizedRevoke = await clientA.DeleteAsync($"/api/Security/sessions/{dataB.SessionId}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedRevoke.StatusCode);

        // 5. User A revokes their own session -> OK
        var ownRevoke = await clientA.DeleteAsync($"/api/Security/sessions/{dataA.SessionId}");
        Assert.Equal(HttpStatusCode.OK, ownRevoke.StatusCode);
    }

    #endregion

    #region 6. Authorization Freshness & Absolute Expiry Tests

    [Fact]
    public async Task Refresh_ReflectsUpdatedRolesAndPermissionsImmediately()
    {
        var client = _fixture.CreateClient();

        // 1. User logs in
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        // 2. Add a new custom permission to User's role in DB
        Guid customRoleId = Guid.NewGuid();
        Guid customPermId = Guid.NewGuid();

        using (var db = GetSecurityDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Id == loginData.UserId);
            var role = new Role
            {
                Id = customRoleId,
                Name = "Role_Freshness_Test_" + Guid.NewGuid().ToString("N")[..6],
                Description = "Freshness test",
                IsActive = true
            };
            db.Roles.Add(role);

            var perm = new Permission
            {
                Id = customPermId,
                Code = "SPECIAL.ACCESS.TEST",
                Module = "Special",
                Description = "Dynamic perm test",
                IsActive = true
            };
            db.Permissions.Add(perm);

            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id
            });

            db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                PermissionId = perm.Id
            });

            await db.SaveChangesAsync();
        }

        try
        {
            // 3. Refresh token
            var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
            {
                RefreshToken = loginData.RefreshToken
            });

            Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
            var refreshData = await refreshRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);

            var perms = refreshData.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
            Assert.Contains("SPECIAL.ACCESS.TEST", perms);
        }
        finally
        {
            // Cleanup custom role & permission
            using var db = GetSecurityDbContext();
            var ur = await db.UserRoles.Where(x => x.RoleId == customRoleId).ToListAsync();
            var rp = await db.RolePermissions.Where(x => x.RoleId == customRoleId).ToListAsync();
            db.UserRoles.RemoveRange(ur);
            db.RolePermissions.RemoveRange(rp);
            var r = await db.Roles.FindAsync(customRoleId);
            var p = await db.Permissions.FindAsync(customPermId);
            if (r != null) db.Roles.Remove(r);
            if (p != null) db.Permissions.Remove(p);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Refresh_AbsoluteSessionExpiryReached_BlocksRefresh()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        // 2. Set absolute session expiry in the past
        using (var db = GetSecurityDbContext())
        {
            var session = await db.UserSessions.FirstAsync(s => s.Id == loginData.SessionId);
            session.AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // 3. Refresh should fail
        var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
        {
            RefreshToken = loginData.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    #endregion

    #region 7. Concurrent Refresh Tests

    [Fact]
    public async Task Refresh_ConcurrentRequests_OnlyOneConsumesSuccessfully()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);

        var tokenToRefresh = loginData.RefreshToken!;

        // 2. Fire two concurrent refresh requests with the exact same refresh token
        var task1 = client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = tokenToRefresh });
        var task2 = client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest { RefreshToken = tokenToRefresh });

        var responses = await Task.WhenAll(task1, task2);

        // Exactly one should succeed, the other must be rejected (either conflict or unauthorized)
        int successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        int failureCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized || r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
    }

    #endregion

    #region 8. Transport Separation, Cookie Policy & CSRF Tests

    [Fact]
    public async Task Refresh_BrowserCookieTransport_RotatesCookieAndDoesNotExposeTokenInBody()
    {
        var client = _fixture.CreateClient();

        // 1. Login to obtain session and cookie
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginCookies = loginRes.Headers.GetValues("Set-Cookie").ToList();
        var rawCookieHeader = loginCookies.FirstOrDefault(c => c.Contains("apex_erp_refresh_token="));
        Assert.NotNull(rawCookieHeader);
        Assert.Contains("samesite=strict", rawCookieHeader, StringComparison.OrdinalIgnoreCase);

        // Extract cookie name and value (e.g. apex_erp_refresh_token=...)
        string cookieToken = rawCookieHeader.Split(';')[0];

        // 2. Perform cookie-based refresh: empty body, Cookie header, valid Origin / custom header
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/Security/refresh")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        refreshReq.Headers.Add("Cookie", cookieToken);
        refreshReq.Headers.Add("Origin", "http://localhost:4200");
        refreshReq.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        // Verify Set-Cookie is returned with rotated token and SameSite=Strict
        Assert.True(refreshRes.Headers.Contains("Set-Cookie"));
        var refreshCookies = refreshRes.Headers.GetValues("Set-Cookie").ToList();
        var rotatedCookieHeader = refreshCookies.FirstOrDefault(c => c.Contains("apex_erp_refresh_token="));
        Assert.NotNull(rotatedCookieHeader);
        Assert.Contains("httponly", rotatedCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", rotatedCookieHeader, StringComparison.OrdinalIgnoreCase);

        // Verify response payload DOES NOT expose refresh token to browser
        var refreshData = await refreshRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.True(refreshData.TryGetProperty("accessToken", out var accessTokenProp));
        Assert.False(string.IsNullOrWhiteSpace(accessTokenProp.GetString()));

        if (refreshData.TryGetProperty("refreshToken", out var refreshTokenProp))
        {
            Assert.True(refreshTokenProp.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(refreshTokenProp.GetString()),
                "Refresh token must NOT be exposed in response body when using browser cookie transport.");
        }
    }

    [Fact]
    public async Task Refresh_BrowserCookieTransport_UntrustedCrossSiteOrigin_ReturnsForbidden()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginCookies = loginRes.Headers.GetValues("Set-Cookie").ToList();
        var rawCookieHeader = loginCookies.First(c => c.Contains("apex_erp_refresh_token="));
        string cookieToken = rawCookieHeader.Split(';')[0];

        // 2. Refresh with untrusted cross-origin
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/Security/refresh")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        refreshReq.Headers.Add("Cookie", cookieToken);
        refreshReq.Headers.Add("Origin", "http://evil.com");

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.Forbidden, refreshRes.StatusCode);
    }

    [Fact]
    public async Task Refresh_FlutterBodyTransport_ReturnsRotatedTokenInBodyAndSetsNoCookie()
    {
        var client = _fixture.CreateClient();

        // 1. Login
        var loginRes = await client.PostAsJsonAsync("/api/Security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginData = await loginRes.ReadApiResponseDataAsync<LoginResponse>(_jsonOpts);
        Assert.NotNull(loginData);
        string bodyToken = loginData.RefreshToken!;

        // 2. Refresh with explicit body transport and NO Cookie header
        var refreshRes = await client.PostAsJsonAsync("/api/Security/refresh", new RefreshTokenRequest
        {
            RefreshToken = bodyToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshData = await refreshRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);

        // Body token must be present and rotated
        string newRefreshToken = refreshData.GetProperty("refreshToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(newRefreshToken));
        Assert.NotEqual(bodyToken, newRefreshToken);

        // No Set-Cookie should be returned for body-only transport
        if (refreshRes.Headers.Contains("Set-Cookie"))
        {
            var cookies = refreshRes.Headers.GetValues("Set-Cookie").ToList();
            Assert.DoesNotContain(cookies, c => c.Contains("apex_erp_refresh_token="));
        }
    }

    [Fact]
    public async Task Refresh_MissingToken_ReturnsUnauthorizedWithoutFallback()
    {
        var client = _fixture.CreateClient();

        // Send refresh with empty JSON and no cookie
        var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/Security/refresh")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    #endregion
}
