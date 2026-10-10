using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Repositories;
using Security.Interfaces;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests;

[Trait("Category", "Integration.Persistence")]
public class SubscriptionUsageEnforcementTests : IAsyncLifetime
{
    private static readonly Guid TenantAId = Guid.Parse("77777777-1111-1111-1111-777777771111");
    private static readonly Guid TenantBId = Guid.Parse("77777777-2222-2222-2222-777777772222");
    private static readonly Guid TenantUnlimitedId = Guid.Parse("77777777-3333-3333-3333-777777773333");
    private static readonly Guid TenantIneligibleId = Guid.Parse("77777777-4444-4444-4444-777777774444");

    private static readonly Guid FinitePlanId = Guid.Parse("77777777-5555-5555-5555-777777775555");
    private static readonly Guid UnlimitedPlanId = Guid.Parse("77777777-6666-6666-6666-777777776666");

    private static readonly Guid SubParamUsersId = Guid.Parse("6535321E-32B7-4403-A2DE-6140B5E0B525");
    private static readonly Guid SubParamMasterDataId = Guid.Parse("79473C93-E54E-4FC1-8749-7CFD881EFD50");

    private static readonly Guid FiniteUsersPlanParamId = Guid.Parse("77777777-aaaa-1111-0000-000000000001");
    private static readonly Guid FiniteMasterDataPlanParamId = Guid.Parse("77777777-aaaa-1111-0000-000000000002");
    private static readonly Guid UnlimitedUsersPlanParamId = Guid.Parse("77777777-bbbb-1111-0000-000000000001");
    private static readonly Guid UnlimitedMasterDataPlanParamId = Guid.Parse("77777777-bbbb-1111-0000-000000000002");

    private static readonly Guid CategoryAId = Guid.Parse("77777777-cccc-1111-0000-000000000001");

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

    private DomainDbContext CreateDomainDb()
    {
        DatabaseSafetyGuard.AssertSafeTestDatabase(TestConstants.DomainDbTest);
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer(TestConstants.DomainDbTest)
            .Options;
        return new DomainDbContext(options);
    }

    private class StubCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }
        public bool IsPlatformUser => !TenantId.HasValue;
        public bool IsTenantUser => TenantId.HasValue;
        public IReadOnlyCollection<Guid> CompanyIds => Array.Empty<Guid>();
        public IReadOnlyCollection<Guid> BranchIds => Array.Empty<Guid>();
    }

    public async Task InitializeAsync()
    {
        await CleanupTestDataAsync();

        using var platformDb = CreatePlatformDb();

        // 1. Seed Tenants
        platformDb.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Enforcement Tenant Alpha", Code = "ENF_A", IsActive = true },
            new Tenant { Id = TenantBId, Name = "Enforcement Tenant Beta", Code = "ENF_B", IsActive = true },
            new Tenant { Id = TenantUnlimitedId, Name = "Enforcement Tenant Unlimited", Code = "ENF_UNL", IsActive = true },
            new Tenant { Id = TenantIneligibleId, Name = "Enforcement Tenant Ineligible", Code = "ENF_INEL", IsActive = true }
        );

        // 2. Seed Plans
        var finitePlan = new SubscriptionPlan
        {
            Id = FinitePlanId,
            Name = "Finite Enforcement Plan",
            Code = "ENF_FINITE",
            Price = 50m,
            IsActive = true
        };

        var unlimitedPlan = new SubscriptionPlan
        {
            Id = UnlimitedPlanId,
            Name = "Unlimited Enforcement Plan",
            Code = "ENF_UNLIMITED",
            Price = 200m,
            IsActive = true
        };

        platformDb.SubscriptionPlans.AddRange(finitePlan, unlimitedPlan);

        // 3. Seed Plan Limits & Parameters
        var finiteLimit = new SubscriptionLimit
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = FinitePlanId,
            IsActive = true
        };
        finiteLimit.Parameters.Add(new SubscriptionPlanParameter
        {
            Id = FiniteUsersPlanParamId,
            SubscriptionLimitId = finiteLimit.Id,
            SubscriptionParameterId = SubParamUsersId,
            LimitValue = 2m, // Finite limit of 2 users
            IsUnlimited = false,
            IsActive = true
        });
        finiteLimit.Parameters.Add(new SubscriptionPlanParameter
        {
            Id = FiniteMasterDataPlanParamId,
            SubscriptionLimitId = finiteLimit.Id,
            SubscriptionParameterId = SubParamMasterDataId,
            LimitValue = 2m, // Finite limit of 2 master data items
            IsUnlimited = false,
            IsActive = true
        });

        var unlimitedLimit = new SubscriptionLimit
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = UnlimitedPlanId,
            IsActive = true
        };
        unlimitedLimit.Parameters.Add(new SubscriptionPlanParameter
        {
            Id = UnlimitedUsersPlanParamId,
            SubscriptionLimitId = unlimitedLimit.Id,
            SubscriptionParameterId = SubParamUsersId,
            LimitValue = null,
            IsUnlimited = true,
            IsActive = true
        });
        unlimitedLimit.Parameters.Add(new SubscriptionPlanParameter
        {
            Id = UnlimitedMasterDataPlanParamId,
            SubscriptionLimitId = unlimitedLimit.Id,
            SubscriptionParameterId = SubParamMasterDataId,
            LimitValue = null,
            IsUnlimited = true,
            IsActive = true
        });

        platformDb.SubscriptionLimits.AddRange(finiteLimit, unlimitedLimit);

        // 4. Seed Subscriptions
        var now = DateTime.UtcNow;
        platformDb.Subscriptions.AddRange(
            new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantAId,
                SubscriptionPlanId = FinitePlanId,
                SubscriptionName = "Enforcement Sub A",
                SubscriptionType = "Paid",
                Status = SubscriptionStatus.Active,
                StartDate = now.AddDays(-1),
                CurrentPeriodStart = now.AddDays(-1),
                CurrentPeriodEnd = now.AddMonths(1),
                BillingAnchorDate = now.AddDays(-1),
                IsActive = true
            },
            new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantBId,
                SubscriptionPlanId = FinitePlanId,
                SubscriptionName = "Enforcement Sub B",
                SubscriptionType = "Paid",
                Status = SubscriptionStatus.Active,
                StartDate = now.AddDays(-1),
                CurrentPeriodStart = now.AddDays(-1),
                CurrentPeriodEnd = now.AddMonths(1),
                BillingAnchorDate = now.AddDays(-1),
                IsActive = true
            },
            new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantUnlimitedId,
                SubscriptionPlanId = UnlimitedPlanId,
                SubscriptionName = "Enforcement Sub Unlimited",
                SubscriptionType = "Paid",
                Status = SubscriptionStatus.Active,
                StartDate = now.AddDays(-1),
                CurrentPeriodStart = now.AddDays(-1),
                CurrentPeriodEnd = now.AddMonths(1),
                BillingAnchorDate = now.AddDays(-1),
                IsActive = true
            },
            new Subscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantIneligibleId,
                SubscriptionPlanId = FinitePlanId,
                SubscriptionName = "Enforcement Sub Expired",
                SubscriptionType = "Paid",
                Status = SubscriptionStatus.Active, // Marked active flag but past due beyond grace period
                StartDate = now.AddMonths(-3),
                EndDate = now.AddDays(-15), // Expired 15 days ago (grace period is 7 days)
                CurrentPeriodStart = now.AddMonths(-3),
                CurrentPeriodEnd = now.AddDays(-15),
                BillingAnchorDate = now.AddMonths(-3),
                IsActive = true
            }
        );

        await platformDb.SaveChangesAsync();

        // 5. Seed Domain Categories
        using var domainDb = CreateDomainDb();
        domainDb.Categories.Add(Category.Create(
            TenantAId,
            "CAT-ENF-A",
            "Enforcement Category A",
            null,
            CategoryAId));
        await domainDb.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestDataAsync();
    }

    private async Task CleanupTestDataAsync()
    {
        var tenantIds = new[] { TenantAId, TenantBId, TenantUnlimitedId, TenantIneligibleId };

        // 1. Cleanup DomainDB_Test
        using var domainDb = CreateDomainDb();
        var testProducts = await domainDb.Products
            .Where(p => tenantIds.Contains(p.TenantId))
            .ToListAsync();
        domainDb.Products.RemoveRange(testProducts);

        var testCategories = await domainDb.Categories
            .Where(c => tenantIds.Contains(c.TenantId))
            .ToListAsync();
        domainDb.Categories.RemoveRange(testCategories);

        await domainDb.SaveChangesAsync();

        // 2. Cleanup SecurityDB_Test
        using var securityDb = CreateSecurityDb();
        var testUsers = await securityDb.Users
            .Where(u => u.TenantId.HasValue && tenantIds.Contains(u.TenantId.Value))
            .ToListAsync();
        securityDb.Users.RemoveRange(testUsers);
        await securityDb.SaveChangesAsync();

        // 3. Cleanup PlatformDB_Test
        using var platformDb = CreatePlatformDb();

        var usages = await platformDb.SubscriptionUsages
            .Where(u => platformDb.Subscriptions.Any(s => tenantIds.Contains(s.TenantId) && s.Id == u.SubscriptionId))
            .ToListAsync();
        platformDb.SubscriptionUsages.RemoveRange(usages);

        var subs = await platformDb.Subscriptions
            .Where(s => tenantIds.Contains(s.TenantId))
            .ToListAsync();
        platformDb.Subscriptions.RemoveRange(subs);

        var planIds = new[] { FinitePlanId, UnlimitedPlanId };
        var planParams = await platformDb.SubscriptionPlanParameters
            .Where(p => platformDb.SubscriptionLimits.Any(l => planIds.Contains(l.SubscriptionPlanId) && l.Id == p.SubscriptionLimitId))
            .ToListAsync();
        platformDb.SubscriptionPlanParameters.RemoveRange(planParams);

        var limits = await platformDb.SubscriptionLimits
            .Where(l => planIds.Contains(l.SubscriptionPlanId))
            .ToListAsync();
        platformDb.SubscriptionLimits.RemoveRange(limits);

        var plans = await platformDb.SubscriptionPlans
            .Where(p => planIds.Contains(p.Id))
            .ToListAsync();
        platformDb.SubscriptionPlans.RemoveRange(plans);

        var tenants = await platformDb.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToListAsync();
        platformDb.Tenants.RemoveRange(tenants);

        await platformDb.SaveChangesAsync();
    }

    [Fact]
    public async Task Users_BelowAndAtLimit_SucceedsAndIncrementsUsage_ThenRejectsAboveLimit()
    {
        using var platformDb = CreatePlatformDb();
        using var securityDb = CreateSecurityDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        var context = new StubCurrentUserContext { TenantId = TenantAId };
        var userService = new UserService(userRepo, context, passwordService, null!, usageService);

        // 1. Below limit: 1st user (0 -> 1)
        var user1 = await userService.CreateUserAsync("alpha_user_1@test.com", "alpha_user_1@test.com", "Password@123");
        Assert.NotNull(user1);

        var subA = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantAId);
        var usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(1m, usage.UsageValue);

        // 2. At limit: 2nd user (1 -> 2)
        var user2 = await userService.CreateUserAsync("alpha_user_2@test.com", "alpha_user_2@test.com", "Password@123");
        Assert.NotNull(user2);

        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(2m, usage.UsageValue);

        // 3. Above limit: 3rd user (2 + 1 > 2) -> rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.CreateUserAsync("alpha_user_3@test.com", "alpha_user_3@test.com", "Password@123"));
        Assert.Contains("Subscription limit exceeded for 'USERS'", ex.Message);

        // Verify usage was not incremented beyond limit
        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(2m, usage.UsageValue);

        // Verify user 3 was NOT persisted in SecurityDB
        var user3InDb = await securityDb.Users.FirstOrDefaultAsync(u => u.Username == "alpha_user_3@test.com");
        Assert.Null(user3InDb);
    }

    [Fact]
    public async Task Products_BelowAndAtLimit_SucceedsAndIncrementsUsage_ThenRejectsAboveLimit()
    {
        using var platformDb = CreatePlatformDb();
        using var domainDb = CreateDomainDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var productRepo = new ProductRepository(domainDb);
        var productService = new ProductService(productRepo, usageService);

        // 1. Below limit: 1st product (0 -> 1)
        var p1 = await productService.CreateProductAsync(TenantAId, new CreateProductRequest
        {
            ProductCode = "PRD-A-01",
            ProductName = "Product Alpha 1",
            CategoryId = CategoryAId
        });
        Assert.NotNull(p1);

        var subA = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantAId);
        var usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteMasterDataPlanParamId);
        Assert.Equal(1m, usage.UsageValue);

        // 2. At limit: 2nd product (1 -> 2)
        var p2 = await productService.CreateProductAsync(TenantAId, new CreateProductRequest
        {
            ProductCode = "PRD-A-02",
            ProductName = "Product Alpha 2",
            CategoryId = CategoryAId
        });
        Assert.NotNull(p2);

        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteMasterDataPlanParamId);
        Assert.Equal(2m, usage.UsageValue);

        // 3. Above limit: 3rd product (2 + 1 > 2) -> rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            productService.CreateProductAsync(TenantAId, new CreateProductRequest
            {
                ProductCode = "PRD-A-03",
                ProductName = "Product Alpha 3",
                CategoryId = CategoryAId
            }));
        Assert.Contains("Subscription limit exceeded for 'MASTER_DATA'", ex.Message);

        // Verify usage was not incremented
        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteMasterDataPlanParamId);
        Assert.Equal(2m, usage.UsageValue);

        // Verify product 3 was NOT persisted in DomainDB
        var p3InDb = await domainDb.Products.FirstOrDefaultAsync(p => p.ProductCode == "PRD-A-03");
        Assert.Null(p3InDb);
    }

    [Fact]
    public async Task UnlimitedParameter_AllowsCreationAndTracksUsageConsistently()
    {
        using var platformDb = CreatePlatformDb();
        using var securityDb = CreateSecurityDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        var context = new StubCurrentUserContext { TenantId = TenantUnlimitedId };
        var userService = new UserService(userRepo, context, passwordService, null!, usageService);

        // Create 3 users for unlimited tenant
        for (int i = 1; i <= 3; i++)
        {
            var user = await userService.CreateUserAsync($"unl_user_{i}@test.com", $"unl_user_{i}@test.com", "Password@123");
            Assert.NotNull(user);
        }

        var subUnl = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantUnlimitedId);
        var usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subUnl.Id && u.SubscriptionPlanParameterId == UnlimitedUsersPlanParamId);
        Assert.Equal(3m, usage.UsageValue);
    }

    [Fact]
    public async Task MissingOrIneligibleSubscription_RejectsCreationSafely()
    {
        using var platformDb = CreatePlatformDb();
        using var securityDb = CreateSecurityDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        // 1. Ineligible (Expired) subscription
        var contextIneligible = new StubCurrentUserContext { TenantId = TenantIneligibleId };
        var userServiceIneligible = new UserService(userRepo, contextIneligible, passwordService, null!, usageService);

        var exExpired = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userServiceIneligible.CreateUserAsync("ineligible@test.com", "ineligible@test.com", "Password@123"));
        Assert.Contains("not entitled to service", exExpired.Message);

        // 2. Missing subscription entirely (unknown tenant)
        var missingTenantId = Guid.NewGuid();
        var contextMissing = new StubCurrentUserContext { TenantId = missingTenantId };
        var userServiceMissing = new UserService(userRepo, contextMissing, passwordService, null!, usageService);

        var exMissing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userServiceMissing.CreateUserAsync("missing@test.com", "missing@test.com", "Password@123"));
        Assert.Contains("Tenant has no active subscription", exMissing.Message);
    }

    [Fact]
    public async Task FailedEntityCreation_DuplicateEmail_DoesNotIncrementUsage()
    {
        using var platformDb = CreatePlatformDb();
        using var securityDb = CreateSecurityDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        var context = new StubCurrentUserContext { TenantId = TenantAId };
        var userService = new UserService(userRepo, context, passwordService, null!, usageService);

        // 1. First user succeeds -> usage is 1
        var user1 = await userService.CreateUserAsync("dup_test@test.com", "dup_test@test.com", "Password@123");
        Assert.NotNull(user1);

        var subA = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantAId);
        var usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(1m, usage.UsageValue);

        // 2. Duplicate user creation fails with InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userService.CreateUserAsync("dup_test@test.com", "dup_test@test.com", "Password@123"));
        Assert.Contains("already registered", ex.Message);

        // 3. Usage remains 1 (transaction rolled back)
        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(1m, usage.UsageValue);
    }

    [Fact]
    public async Task FailedEntityCreation_DuplicateProductCode_DoesNotIncrementUsage()
    {
        using var platformDb = CreatePlatformDb();
        using var domainDb = CreateDomainDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var productRepo = new ProductRepository(domainDb);
        var productService = new ProductService(productRepo, usageService);

        // 1. First product succeeds -> usage is 1
        var p1 = await productService.CreateProductAsync(TenantAId, new CreateProductRequest
        {
            ProductCode = "DUP-CODE-01",
            ProductName = "Unique Name 1",
            CategoryId = CategoryAId
        });
        Assert.NotNull(p1);

        var subA = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantAId);
        var usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteMasterDataPlanParamId);
        Assert.Equal(1m, usage.UsageValue);

        // 2. Duplicate product code fails
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            productService.CreateProductAsync(TenantAId, new CreateProductRequest
            {
                ProductCode = "DUP-CODE-01",
                ProductName = "Unique Name 2",
                CategoryId = CategoryAId
            }));
        Assert.Contains("already exists", ex.Message);

        // 3. Usage remains 1
        usage = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id && u.SubscriptionPlanParameterId == FiniteMasterDataPlanParamId);
        Assert.Equal(1m, usage.UsageValue);
    }

    [Fact]
    public async Task TenantIsolation_ExhaustedTenantADoesNotAffectTenantB()
    {
        using var platformDb = CreatePlatformDb();
        using var securityDb = CreateSecurityDb();

        var usageService = new SubscriptionUsageService(platformDb);
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        // 1. Exhaust Tenant A limit (2/2)
        var contextA = new StubCurrentUserContext { TenantId = TenantAId };
        var userServiceA = new UserService(userRepo, contextA, passwordService, null!, usageService);

        await userServiceA.CreateUserAsync("tenant_a_1@test.com", "tenant_a_1@test.com", "Password@123");
        await userServiceA.CreateUserAsync("tenant_a_2@test.com", "tenant_a_2@test.com", "Password@123");

        // Tenant A is now blocked
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            userServiceA.CreateUserAsync("tenant_a_3@test.com", "tenant_a_3@test.com", "Password@123"));

        // 2. Tenant B has its own independent quota and can create users
        var contextB = new StubCurrentUserContext { TenantId = TenantBId };
        var userServiceB = new UserService(userRepo, contextB, passwordService, null!, usageService);

        var userB = await userServiceB.CreateUserAsync("tenant_b_1@test.com", "tenant_b_1@test.com", "Password@123");
        Assert.NotNull(userB);

        var subB = await platformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantBId);
        var usageB = await platformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subB.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);
        Assert.Equal(1m, usageB.UsageValue);
    }

    [Fact]
    public async Task PlatformUser_CreatesUserWithoutTenantSubscriptionEnforcement()
    {
        using var securityDb = CreateSecurityDb();
        var userRepo = new UserRepository(securityDb);
        var passwordService = new PasswordService();

        // Platform user context (TenantId = null)
        var platformContext = new StubCurrentUserContext { TenantId = null };
        var userService = new UserService(userRepo, platformContext, passwordService, null!, null);

        var platformUser = await userService.CreateUserAsync("plat_admin_created@test.com", "plat_admin_created@test.com", "Password@123");
        Assert.NotNull(platformUser);
        Assert.Null(platformUser.TenantId);

        // Clean up created platform user
        securityDb.Users.Remove(platformUser);
        await securityDb.SaveChangesAsync();
    }

    [Fact]
    public async Task ConcurrentRequests_CannotExceedFiniteLimit()
    {
        // Tenant B has limit of 2 users
        // Launch 6 concurrent creation tasks simultaneously
        int totalRequests = 6;
        var results = new List<Task<User?>>();

        for (int i = 1; i <= totalRequests; i++)
        {
            int index = i;
            results.Add(Task.Run(async () =>
            {
                using var pDb = CreatePlatformDb();
                using var sDb = CreateSecurityDb();
                var uService = new SubscriptionUsageService(pDb);
                var uRepo = new UserRepository(sDb);
                var pService = new PasswordService();
                var ctx = new StubCurrentUserContext { TenantId = TenantBId };
                var usrService = new UserService(uRepo, ctx, pService, null!, uService);

                try
                {
                    return await usrService.CreateUserAsync($"concurrent_b_{index}@test.com", $"concurrent_b_{index}@test.com", "Password@123");
                }
                catch (InvalidOperationException)
                {
                    return null; // Rejected due to limit exceeded
                }
            }));
        }

        var completed = await Task.WhenAll(results);
        int successCount = completed.Count(u => u != null);
        int rejectedCount = completed.Count(u => u == null);

        // Exactly 2 must succeed (limit = 2) and exactly 4 rejected
        Assert.Equal(2, successCount);
        Assert.Equal(4, rejectedCount);

        using var verifyPlatformDb = CreatePlatformDb();
        var subB = await verifyPlatformDb.Subscriptions.FirstAsync(s => s.TenantId == TenantBId);
        var usageB = await verifyPlatformDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subB.Id && u.SubscriptionPlanParameterId == FiniteUsersPlanParamId);

        // Verified: Usage value is exactly 2, never exceeded under concurrency
        Assert.Equal(2m, usageB.UsageValue);

        using var verifySecurityDb = CreateSecurityDb();
        int actualUsersInDb = await verifySecurityDb.Users
            .CountAsync(u => u.TenantId == TenantBId && u.Username.StartsWith("concurrent_b_"));
        Assert.Equal(2, actualUsersInDb);
    }
}
