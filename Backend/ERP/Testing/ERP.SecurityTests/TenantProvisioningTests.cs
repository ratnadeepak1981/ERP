using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Application.DTOs;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests;

public class TenantProvisioningTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public TenantProvisioningTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private SaaSDbContext CreatePlatformDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.PlatformDbTest);
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseSqlServer(TestConstants.PlatformDbTest)
            .Options;
        return new SaaSDbContext(options);
    }

    private SecurityDbContext CreateSecurityDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.SecurityDbTest);
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlServer(TestConstants.SecurityDbTest)
            .Options;
        return new SecurityDbContext(options);
    }

    private async Task CleanupTestTenantAsync(Guid tenantId, string username)
    {
        using var platformDb = CreatePlatformDb();
        var dbs = await platformDb.TenantDatabases.Where(x => x.TenantId == tenantId).ToListAsync();
        platformDb.TenantDatabases.RemoveRange(dbs);

        var configs = await platformDb.TenantConfigurations.Where(x => x.TenantId == tenantId).ToListAsync();
        platformDb.TenantConfigurations.RemoveRange(configs);

        var usages = await platformDb.SubscriptionUsages.Where(x => x.Subscription.TenantId == tenantId).ToListAsync();
        platformDb.SubscriptionUsages.RemoveRange(usages);

        var subs = await platformDb.Subscriptions.Where(x => x.TenantId == tenantId).ToListAsync();
        platformDb.Subscriptions.RemoveRange(subs);

        var tenants = await platformDb.Tenants.Where(x => x.Id == tenantId).ToListAsync();
        platformDb.Tenants.RemoveRange(tenants);

        await platformDb.SaveChangesAsync();

        using var secDb = CreateSecurityDb();
        var users = await secDb.Users
            .Include(u => u.UserRoles)
            .Where(u => u.TenantId == tenantId || (tenantId == Guid.Empty && u.Username == username))
            .ToListAsync();

        foreach (var u in users)
        {
            secDb.UserRoles.RemoveRange(u.UserRoles);
        }
        secDb.Users.RemoveRange(users);
        await secDb.SaveChangesAsync();
    }

    [Fact]
    public async Task ProvisionTenant_ValidUnprovisionedTenant_Succeeds_AndPersistsBothRecordsAndAdminIdentity()
    {
        var testTenantId = Guid.NewGuid();
        var adminUser = $"provadmin_{Guid.NewGuid():N}"[..12];
        var adminEmail = $"{adminUser}@tenantprov.local";

        try
        {
            // Arrange: Seed tenant with active shared subscription
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = testTenantId,
                    Name = "Provisioning Test Corp",
                    Code = $"PROV_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });

                var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                    ?? throw new InvalidOperationException("FREE plan must exist.");

                platformDb.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = testTenantId,
                    SubscriptionPlanId = freePlan.Id,
                    SubscriptionName = "Provision Test Subscription",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StorageMode = TenantStorageMode.Shared,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });

                await platformDb.SaveChangesAsync();
            }

            var client = _fixture.CreateClient();
            var req = new TenantProvisioningRequest
            {
                TenantId = testTenantId,
                AdminUserName = adminUser,
                AdminEmail = adminEmail,
                AdminPassword = "SecurePassword123!"
            };

            // Act: Call POST /api/tenant/provision
            var response = await client.PostAsJsonAsync("/api/tenant/provision", req);

            // Assert API response
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<TenantProvisioningResult>(_jsonOpts);
            Assert.NotNull(result);
            Assert.True(result.IsProvisioned);
            Assert.True(result.DatabaseProvisioned);
            Assert.True(result.ConfigurationProvisioned);
            Assert.Equal("Shared", result.StorageMode);

            // Verify PlatformDB persistence: TenantDatabase
            using (var platformDb = CreatePlatformDb())
            {
                var tenantDb = await platformDb.TenantDatabases.FirstOrDefaultAsync(x => x.TenantId == testTenantId);
                Assert.NotNull(tenantDb);
                Assert.Equal("DomainDB", tenantDb.DatabaseName);
                Assert.True(tenantDb.IsActive);

                // Verify PlatformDB persistence: TenantConfiguration
                var tenantConfig = await platformDb.TenantConfigurations.FirstOrDefaultAsync(x => x.TenantId == testTenantId);
                Assert.NotNull(tenantConfig);
                Assert.True(tenantConfig.CompanyEnabled);
                Assert.True(tenantConfig.BranchEnabled);
            }

            // Verify SecurityDB persistence: Tenant Admin user and role assignment
            using (var secDb = CreateSecurityDb())
            {
                var user = await secDb.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.TenantId == testTenantId && u.Username == adminUser);

                Assert.NotNull(user);
                Assert.Equal(adminEmail, user.Email);
                Assert.True(user.IsActive);
                Assert.NotEmpty(user.PasswordHash);
                Assert.NotEqual("SecurePassword123!", user.PasswordHash); // Password must be hashed

                var userRole = user.UserRoles.FirstOrDefault(ur => ur.Role.Name == "Tenant Admin");
                Assert.NotNull(userRole);
            }
        }
        finally
        {
            await CleanupTestTenantAsync(testTenantId, adminUser);
        }
    }

    [Fact]
    public async Task ProvisionTenant_RepeatedCall_ReturnsConflict()
    {
        var testTenantId = Guid.NewGuid();
        var adminUser = $"provadmin_{Guid.NewGuid():N}"[..12];

        try
        {
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = testTenantId,
                    Name = "Repeat Provisioning Corp",
                    Code = $"REP_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });

                var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE");

                platformDb.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = testTenantId,
                    SubscriptionPlanId = freePlan!.Id,
                    SubscriptionName = "Repeat Sub",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StorageMode = TenantStorageMode.Shared,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });

                await platformDb.SaveChangesAsync();
            }

            var client = _fixture.CreateClient();
            var req = new TenantProvisioningRequest
            {
                TenantId = testTenantId,
                AdminUserName = adminUser,
                AdminEmail = $"{adminUser}@repeat.local",
                AdminPassword = "Password123!"
            };

            // First provision -> Succeeds
            var firstRes = await client.PostAsJsonAsync("/api/tenant/provision", req);
            Assert.Equal(HttpStatusCode.OK, firstRes.StatusCode);

            // Second provision -> Fails with 409 Conflict
            var secondRes = await client.PostAsJsonAsync("/api/tenant/provision", req);
            Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
        }
        finally
        {
            await CleanupTestTenantAsync(testTenantId, adminUser);
        }
    }

    [Fact]
    public async Task ProvisionTenant_NonExistentTenant_ReturnsConflict()
    {
        var client = _fixture.CreateClient();
        var req = new TenantProvisioningRequest
        {
            TenantId = Guid.NewGuid(),
            AdminUserName = "ghost_admin",
            AdminEmail = "ghost@tenant.local",
            AdminPassword = "Password123!"
        };

        var response = await client.PostAsJsonAsync("/api/tenant/provision", req);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ProvisionTenant_NoActiveSubscription_ReturnsConflict()
    {
        var testTenantId = Guid.NewGuid();

        try
        {
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = testTenantId,
                    Name = "Inactive Sub Corp",
                    Code = $"INACT_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });
                await platformDb.SaveChangesAsync();
            }

            var client = _fixture.CreateClient();
            var req = new TenantProvisioningRequest
            {
                TenantId = testTenantId,
                AdminUserName = "sub_admin",
                AdminEmail = "sub@inactive.local",
                AdminPassword = "Password123!"
            };

            var response = await client.PostAsJsonAsync("/api/tenant/provision", req);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await CleanupTestTenantAsync(testTenantId, "sub_admin");
        }
    }

    [Fact]
    public async Task EnsureTenantAdminAsync_Idempotent_DoesNotDuplicateUserOrRole()
    {
        using var scope = _fixture.Services.CreateScope();
        var securityBootstrap = scope.ServiceProvider.GetRequiredService<Security.Services.SecurityBootstrapService>();
        var testTenantId = Guid.NewGuid();
        var username = $"idemp_{Guid.NewGuid():N}"[..12];
        var email = $"{username}@test.local";

        try
        {
            await securityBootstrap.EnsureTenantAdminRoleAsync();

            // First call
            await securityBootstrap.EnsureTenantAdminAsync(testTenantId, username, email, "Pass123!");

            // Second call (idempotent)
            await securityBootstrap.EnsureTenantAdminAsync(testTenantId, username, email, "Pass123!");

            // Assert in database: Exactly 1 user and 1 user-role
            using var secDb = CreateSecurityDb();
            var users = await secDb.Users
                .Include(u => u.UserRoles)
                .Where(u => u.TenantId == testTenantId && u.Username == username)
                .ToListAsync();

            Assert.Single(users);
            Assert.Single(users[0].UserRoles);
        }
        finally
        {
            await CleanupTestTenantAsync(testTenantId, username);
        }
    }

    [Fact]
    public async Task ProvisionTenant_WhenSecurityBootstrapFails_RollsBackPlatformDbRecords()
    {
        var testTenantId = Guid.NewGuid();
        var invalidUsername = ""; // Empty username will fail ArgumentException during Security bootstrap

        try
        {
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = testTenantId,
                    Name = "Rollback Test Corp",
                    Code = $"ROLL_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });

                var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE");

                platformDb.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = testTenantId,
                    SubscriptionPlanId = freePlan!.Id,
                    SubscriptionName = "Rollback Sub",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StorageMode = TenantStorageMode.Shared,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });

                await platformDb.SaveChangesAsync();
            }

            var client = _fixture.CreateClient();
            var req = new TenantProvisioningRequest
            {
                TenantId = testTenantId,
                AdminUserName = invalidUsername,
                AdminEmail = "rollback@test.local",
                AdminPassword = "Password123!"
            };

            // Call provision -> Security bootstrap throws ArgumentException
            var response = await client.PostAsJsonAsync("/api/tenant/provision", req);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // Assert compensation rollback in PlatformDB: TenantDatabase and TenantConfiguration should NOT exist!
            using (var platformDb = CreatePlatformDb())
            {
                var tenantDb = await platformDb.TenantDatabases.FirstOrDefaultAsync(x => x.TenantId == testTenantId);
                var tenantConfig = await platformDb.TenantConfigurations.FirstOrDefaultAsync(x => x.TenantId == testTenantId);

                Assert.Null(tenantDb);
                Assert.Null(tenantConfig);
            }
        }
        finally
        {
            await CleanupTestTenantAsync(testTenantId, invalidUsername);
        }
    }
}
