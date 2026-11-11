using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Security.Application.DTOs;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class UserControllerAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UserControllerAuthorizationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private HttpClient CreateClient(Guid? tenantId, string[] roles, string[] permissions)
    {
        return _fixture.CreateAuthenticatedClient(
            userId: Guid.NewGuid(),
            username: "sec_test_user",
            tenantId: tenantId,
            roles: roles,
            permissions: permissions);
    }

    [Fact]
    public async Task Unauthenticated_GetUsers_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithoutUserView_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: Array.Empty<string>());

        var response = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithUserView_ReturnsOk()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW" });

        var response = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(data);
    }

    [Fact]
    public async Task GetUserById_WithoutUserView_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: Array.Empty<string>());

        var response = await client.GetAsync($"/api/user/{TestConstants.TenantUserAlphaId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_WithUserView_ReturnsOk()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW" });

        var response = await client.GetAsync($"/api/user/{TestConstants.TenantUserAlphaId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(TestConstants.TenantAlphaId, data.GetProperty("tenantId").GetGuid());
    }

    [Fact]
    public async Task CreateTenantUser_WithoutUserCreate_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW" });

        var response = await client.PostAsJsonAsync("/api/user/tenant-user", new CreateUserRequest
        {
            Username = "unauthorized_user",
            Email = $"unauth_{Guid.NewGuid():N}@test.local",
            Password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePlatformUser_WithUserCreate_CreatesUserSuccessfully()
    {
        var client = CreateClient(
            tenantId: null, // Platform scope
            roles: new[] { "Platform Admin" },
            permissions: new[] { "USER.CREATE", "USER.VIEW" });

        var uniqueEmail = $"platform_user_{Guid.NewGuid():N}@platform.local";

        var response = await client.PostAsJsonAsync("/api/user", new CreateUserRequest
        {
            Username = $"plt_{Guid.NewGuid():N}",
            Email = uniqueEmail,
            Password = "Password@123"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(uniqueEmail, created.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UpdateUser_WithoutUserEdit_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW" });

        var response = await client.PutAsJsonAsync($"/api/user/{TestConstants.TenantUserAlphaId}", new UpdateUserRequest
        {
            Username = "attempted_update"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_WithUserEdit_UpdatesSuccessfully()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom HR Officer" },
            permissions: new[] { "USER.EDIT", "USER.VIEW" });

        var response = await client.PutAsJsonAsync($"/api/user/{TestConstants.TenantUserAlphaId}", new UpdateUserRequest
        {
            Username = "updated_name"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ToggleUserStatus_Deactivation_WithoutUserDelete_ReturnsForbidden()
    {
        // Target existing active user
        var targetUserId = TestConstants.TenantUserAlphaId;

        // Caller with only USER.EDIT (no USER.DELETE) attempts to deactivate active user -> Forbidden
        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Editor" },
            permissions: new[] { "USER.EDIT" });

        var toggleRes = await editorClient.PutAsync($"/api/user/{targetUserId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.Forbidden, toggleRes.StatusCode);
    }

    [Fact]
    public async Task ToggleUserStatus_Lifecycle_DeactivateAndReactivate_Succeeds()
    {
        var targetUserId = TestConstants.TenantUserAlphaId;

        // 1. Caller with USER.DELETE deactivates the user
        var deleteClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Officer" },
            permissions: new[] { "USER.DELETE" });

        var deactRes = await deleteClient.PutAsync($"/api/user/{targetUserId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);

        var deactData = await deactRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.False(deactData.GetProperty("isActive").GetBoolean());
        Assert.Equal("Suspended", deactData.GetProperty("accountStatus").GetString());

        // 2. Caller with only USER.DELETE attempts to reactivate the suspended user -> Forbidden
        var forbiddenReactivateRes = await deleteClient.PutAsync($"/api/user/{targetUserId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReactivateRes.StatusCode);

        // 3. Caller with USER.EDIT reactivates the suspended user -> OK
        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Editor" },
            permissions: new[] { "USER.EDIT" });

        var reactRes = await editorClient.PutAsync($"/api/user/{targetUserId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, reactRes.StatusCode);

        var reactData = await reactRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.True(reactData.GetProperty("isActive").GetBoolean());
        Assert.Equal("Active", reactData.GetProperty("accountStatus").GetString());
    }

    [Fact]
    public async Task CustomRole_WithUserView_WorksWithoutAdminRoles()
    {
        // Demonstrates custom non-admin role support through permission claims
        var customClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Onboarding Lead" },
            permissions: new[] { "USER.VIEW" });

        var getRes = await customClient.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var getUserRes = await customClient.GetAsync($"/api/user/{TestConstants.TenantUserAlphaId}");
        Assert.Equal(HttpStatusCode.OK, getUserRes.StatusCode);
    }
}
