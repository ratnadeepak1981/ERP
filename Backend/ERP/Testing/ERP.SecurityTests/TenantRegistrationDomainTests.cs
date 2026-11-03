using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;
using Security.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests;

public class TenantRegistrationDomainTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public TenantRegistrationDomainTests(TestSecurityFixture fixture)
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

    private async Task CleanupTestTenantAsync(Guid tenantId, string? username = null)
    {
        using var platformDb = CreatePlatformDb();

        var domains = await platformDb.TenantDomains.Where(x => x.TenantId == tenantId).ToListAsync();
        platformDb.TenantDomains.RemoveRange(domains);

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

        if (!string.IsNullOrEmpty(username))
        {
            using var secDb = CreateSecurityDb();
            var users = await secDb.Users
                .Include(u => u.UserRoles)
                .Where(u => u.TenantId == tenantId || u.Username == username)
                .ToListAsync();

            foreach (var u in users)
            {
                secDb.UserRoles.RemoveRange(u.UserRoles);
            }
            secDb.Users.RemoveRange(users);
            await secDb.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RegisterTenant_WithExplicitValidSubdomain_CreatesAndPersistsPrimaryTenantDomain()
    {
        Guid tenantId = Guid.Empty;
        string uniqueSub = $"acme{Guid.NewGuid():N}"[..12];

        try
        {
            using var scope = _fixture.Services.CreateScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

            using var platformDb = CreatePlatformDb();
            var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                ?? throw new InvalidOperationException("FREE plan not found.");

            var request = new CreateTenantRequest
            {
                Name = $"Acme Precision {Guid.NewGuid():N}"[..20],
                SubscriptionPlanId = freePlan.Id,
                Subdomain = uniqueSub
            };

            var result = await tenantService.RegisterTenant(request);

            Assert.NotNull(result);
            Assert.NotNull(result.Tenant);
            tenantId = result.Tenant.Id;

            Assert.NotNull(result.PrimaryDomain);
            Assert.Equal(uniqueSub, result.Subdomain);
            Assert.Equal($"{uniqueSub}.localhost", result.Hostname);
            Assert.Equal($"{uniqueSub}.localhost", result.PrimaryDomain.Hostname);
            Assert.True(result.PrimaryDomain.IsPrimary);
            Assert.True(result.PrimaryDomain.IsActive);
            Assert.True(result.PrimaryDomain.IsVerified);

            // Verify in PlatformDB
            using var verifyDb = CreatePlatformDb();
            var domainRecord = await verifyDb.TenantDomains.FirstOrDefaultAsync(x => x.TenantId == tenantId);
            Assert.NotNull(domainRecord);
            Assert.Equal($"{uniqueSub}.localhost", domainRecord.Hostname);
            Assert.Equal(uniqueSub, domainRecord.Subdomain);
            Assert.True(domainRecord.IsPrimary);
        }
        finally
        {
            if (tenantId != Guid.Empty)
            {
                await CleanupTestTenantAsync(tenantId);
            }
        }
    }

    [Fact]
    public async Task RegisterTenant_WithReservedSubdomain_ThrowsArgumentException()
    {
        using var scope = _fixture.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        using var platformDb = CreatePlatformDb();
        var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
            ?? throw new InvalidOperationException("FREE plan not found.");

        var request = new CreateTenantRequest
        {
            Name = "Reserved Tenant Org",
            SubscriptionPlanId = freePlan.Id,
            Subdomain = "admin"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tenantService.RegisterTenant(request));
        Assert.Contains("invalid or reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterTenant_WithInvalidSubdomainCharacters_ThrowsArgumentException()
    {
        using var scope = _fixture.Services.CreateScope();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        using var platformDb = CreatePlatformDb();
        var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
            ?? throw new InvalidOperationException("FREE plan not found.");

        var request = new CreateTenantRequest
        {
            Name = "Invalid Char Org",
            SubscriptionPlanId = freePlan.Id,
            Subdomain = "-invalid-sub-"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => tenantService.RegisterTenant(request));
        Assert.Contains("invalid or reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterTenant_WithDuplicateSubdomain_ThrowsInvalidOperationException()
    {
        Guid tenantId1 = Guid.Empty;
        string duplicateSub = $"dup{Guid.NewGuid():N}"[..10];

        try
        {
            using var scope = _fixture.Services.CreateScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

            using var platformDb = CreatePlatformDb();
            var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                ?? throw new InvalidOperationException("FREE plan not found.");

            var request1 = new CreateTenantRequest
            {
                Name = $"First Org {Guid.NewGuid():N}"[..18],
                SubscriptionPlanId = freePlan.Id,
                Subdomain = duplicateSub
            };

            var result1 = await tenantService.RegisterTenant(request1);
            tenantId1 = result1.Tenant.Id;

            // Attempt duplicate with second tenant
            using var scope2 = _fixture.Services.CreateScope();
            var tenantService2 = scope2.ServiceProvider.GetRequiredService<ITenantService>();

            var request2 = new CreateTenantRequest
            {
                Name = $"Second Org {Guid.NewGuid():N}"[..18],
                SubscriptionPlanId = freePlan.Id,
                Subdomain = duplicateSub
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tenantService2.RegisterTenant(request2));
            Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (tenantId1 != Guid.Empty)
            {
                await CleanupTestTenantAsync(tenantId1);
            }
        }
    }

    [Fact]
    public async Task RegisterTenant_WithoutSubdomain_AutoDerivesValidCandidate()
    {
        Guid tenantId = Guid.Empty;

        try
        {
            using var scope = _fixture.Services.CreateScope();
            var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

            using var platformDb = CreatePlatformDb();
            var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                ?? throw new InvalidOperationException("FREE plan not found.");

            string uniqueOrgName = $"Global Logistics {Guid.NewGuid():N}"[..24];
            var request = new CreateTenantRequest
            {
                Name = uniqueOrgName,
                SubscriptionPlanId = freePlan.Id,
                Subdomain = null // Omitted
            };

            var result = await tenantService.RegisterTenant(request);

            Assert.NotNull(result);
            tenantId = result.Tenant.Id;
            Assert.NotNull(result.PrimaryDomain);
            Assert.False(string.IsNullOrWhiteSpace(result.Subdomain));
            Assert.Equal($"{result.Subdomain}.localhost", result.Hostname);

            // Ensure derived subdomain is valid
            Assert.True(TenantDomainRules.IsValidSubdomain(result.Subdomain));
        }
        finally
        {
            if (tenantId != Guid.Empty)
            {
                await CleanupTestTenantAsync(tenantId);
            }
        }
    }

    [Fact]
    public async Task ProvisionTenant_WhenDomainAlreadyExists_LeavesExistingDomainIntact()
    {
        Guid tenantId = Guid.NewGuid();
        string existingSub = $"preexist{Guid.NewGuid():N}"[..14];
        string adminUser = $"prov_{Guid.NewGuid():N}"[..12];

        try
        {
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = $"Pre-registered {Guid.NewGuid():N}"[..22],
                    Code = $"PR-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });

                var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                    ?? throw new InvalidOperationException("FREE plan not found.");

                platformDb.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubscriptionPlanId = freePlan.Id,
                    SubscriptionName = "Free Subscription",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StorageMode = TenantStorageMode.Shared,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });

                platformDb.TenantDomains.Add(new TenantDomain
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Hostname = $"{existingSub}.localhost",
                    Subdomain = existingSub,
                    DomainType = TenantDomainType.Subdomain,
                    IsPrimary = true,
                    IsActive = true,
                    IsVerified = true
                });

                await platformDb.SaveChangesAsync();
            }

            using var scope = _fixture.Services.CreateScope();
            var provisioningService = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

            var req = new TenantProvisioningRequest
            {
                TenantId = tenantId,
                AdminUserName = adminUser,
                AdminEmail = $"{adminUser}@tenantdomain.local",
                AdminPassword = "SecurePassword123!"
            };

            var result = await provisioningService.ProvisionTenant(req);

            Assert.NotNull(result);
            Assert.True(result.IsProvisioned);
            Assert.True(result.DomainProvisioned);
            Assert.Equal($"{existingSub}.localhost", result.Hostname);
            Assert.Equal(existingSub, result.Subdomain);

            // Verify only one domain exists in database
            using (var verifyDb = CreatePlatformDb())
            {
                var domains = await verifyDb.TenantDomains.Where(x => x.TenantId == tenantId).ToListAsync();
                Assert.Single(domains);
                Assert.Equal(existingSub, domains[0].Subdomain);
            }
        }
        finally
        {
            await CleanupTestTenantAsync(tenantId, adminUser);
        }
    }

    [Fact]
    public async Task ProvisionTenant_WhenNoDomainExists_ProvisionsDefaultDomain()
    {
        Guid tenantId = Guid.NewGuid();
        string adminUser = $"prov_{Guid.NewGuid():N}"[..12];

        try
        {
            using (var platformDb = CreatePlatformDb())
            {
                platformDb.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = $"Legacy Tenant {Guid.NewGuid():N}"[..22],
                    Code = $"LEG-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                    IsActive = true
                });

                var freePlan = await platformDb.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE")
                    ?? throw new InvalidOperationException("FREE plan not found.");

                platformDb.Subscriptions.Add(new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SubscriptionPlanId = freePlan.Id,
                    SubscriptionName = "Legacy Subscription",
                    SubscriptionType = "FREE",
                    Status = SubscriptionStatus.Active,
                    StorageMode = TenantStorageMode.Shared,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    IsActive = true
                });

                await platformDb.SaveChangesAsync();
            }

            using var scope = _fixture.Services.CreateScope();
            var provisioningService = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();

            var req = new TenantProvisioningRequest
            {
                TenantId = tenantId,
                AdminUserName = adminUser,
                AdminEmail = $"{adminUser}@tenantdomain.local",
                AdminPassword = "SecurePassword123!"
            };

            var result = await provisioningService.ProvisionTenant(req);

            Assert.NotNull(result);
            Assert.True(result.IsProvisioned);
            Assert.True(result.DomainProvisioned);
            Assert.False(string.IsNullOrWhiteSpace(result.Hostname));
            Assert.False(string.IsNullOrWhiteSpace(result.Subdomain));

            // Verify domain was persisted in PlatformDB
            using (var verifyDb = CreatePlatformDb())
            {
                var domain = await verifyDb.TenantDomains.FirstOrDefaultAsync(x => x.TenantId == tenantId);
                Assert.NotNull(domain);
                Assert.True(domain.IsPrimary);
                Assert.Equal(result.Hostname, domain.Hostname);
            }
        }
        finally
        {
            await CleanupTestTenantAsync(tenantId, adminUser);
        }
    }
}
