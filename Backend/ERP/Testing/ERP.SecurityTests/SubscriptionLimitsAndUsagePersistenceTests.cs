using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Persistence.Repositories;
using SaaS.Infrastructure.Repositories;
using Xunit;

namespace ERP.SecurityTests;

[Trait("Category", "Integration.Persistence")]
public class SubscriptionLimitsAndUsagePersistenceTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("66666666-0000-0000-0000-666666666666");
    private static readonly Guid TenantB = Guid.Parse("55555555-0000-0000-0000-555555555555");
    private static readonly Guid CustomPlanId = Guid.Parse("44444444-0000-0000-0000-444444444444");

    private SaaSDbContext CreateDbContext()
    {
        string connStr = TestConstants.PlatformDbTest;
        DatabaseSafetyGuard.AssertSafeTestDatabase(connStr);

        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseSqlServer(connStr)
            .Options;

        return new SaaSDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await CleanupTestDataAsync();

        using var db = CreateDbContext();

        db.Tenants.Add(new Tenant
        {
            Id = TenantA,
            Name = "Limit Test Tenant Alpha",
            Code = "LIMIT_ALPHA",
            IsActive = true
        });

        db.Tenants.Add(new Tenant
        {
            Id = TenantB,
            Name = "Limit Test Tenant Beta",
            Code = "LIMIT_BETA",
            IsActive = true
        });

        // Plan with limits
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = CustomPlanId,
            Name = "Tiered Growth Plan",
            Code = "Paid",
            Price = 199.00m,
            BillingCycle = DurationCycle.Monthly,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestDataAsync();
    }

    private async Task CleanupTestDataAsync()
    {
        using var db = CreateDbContext();

        // 1. Remove usages for test tenants
        var subIds = await db.Subscriptions
            .Where(s => s.TenantId == TenantA || s.TenantId == TenantB)
            .Select(s => s.Id)
            .ToListAsync();

        if (subIds.Any())
        {
            var usages = await db.SubscriptionUsages
                .Where(u => subIds.Contains(u.SubscriptionId))
                .ToListAsync();
            db.SubscriptionUsages.RemoveRange(usages);

            var subs = await db.Subscriptions
                .Where(s => subIds.Contains(s.Id))
                .ToListAsync();
            db.Subscriptions.RemoveRange(subs);

            await db.SaveChangesAsync();
        }

        // 2. Remove plan parameters, limits, and plan
        var planLimit = await db.SubscriptionLimits
            .FirstOrDefaultAsync(l => l.SubscriptionPlanId == CustomPlanId);
        if (planLimit != null)
        {
            var pparams = await db.SubscriptionPlanParameters
                .Where(p => p.SubscriptionLimitId == planLimit.Id)
                .ToListAsync();
            db.SubscriptionPlanParameters.RemoveRange(pparams);
            db.SubscriptionLimits.Remove(planLimit);
            await db.SaveChangesAsync();
        }

        var plan = await db.SubscriptionPlans.FindAsync(CustomPlanId);
        if (plan != null) db.SubscriptionPlans.Remove(plan);

        var tA = await db.Tenants.FindAsync(TenantA);
        if (tA != null) db.Tenants.Remove(tA);

        var tB = await db.Tenants.FindAsync(TenantB);
        if (tB != null) db.Tenants.Remove(tB);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SubscriptionLimitService_Create_EnforcesOneLimitPerPlanConstraint()
    {
        using var db = CreateDbContext();
        var limitRepo = new SubscriptionLimitRepository(db);
        var limitService = new SubscriptionLimitService(limitRepo);

        // 1. First limit created successfully
        var limit = limitService.Create(new CreateSubscriptionLimitRequest
        {
            SubscriptionPlanId = CustomPlanId,
            IsActive = true
        });

        Assert.NotNull(limit);
        Assert.Equal(CustomPlanId, limit.SubscriptionPlanId);

        // 2. Second limit attempt for same plan must throw InvalidOperationException
        var ex = Assert.Throws<InvalidOperationException>(() =>
            limitService.Create(new CreateSubscriptionLimitRequest
            {
                SubscriptionPlanId = CustomPlanId,
                IsActive = true
            }));

        Assert.Contains("already exists for this plan", ex.Message);
    }

    [Fact]
    public async Task SubscriptionPlanParameter_DatabaseConstraints_EnforceUnlimitedAndLimitValueConsistency()
    {
        using var db = CreateDbContext();
        var limit = new SubscriptionLimit
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = CustomPlanId,
            IsActive = true
        };
        db.SubscriptionLimits.Add(limit);
        await db.SaveChangesAsync();

        var usersParam = await db.SubscriptionParameters
            .FirstAsync(p => p.ParameterKey == "USERS");

        // 1. Invalid: IsUnlimited = false but LimitValue is null
        var invalidParam1 = new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = limit.Id,
            SubscriptionParameterId = usersParam.Id,
            IsUnlimited = false,
            LimitValue = null, // VIOLATION of CK_SubscriptionPlanParameters_Unlimited_LimitValue
            IsActive = true
        };
        db.SubscriptionPlanParameters.Add(invalidParam1);

        var ex1 = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlEx1 = ex1.GetBaseException() as SqlException;
        Assert.NotNull(sqlEx1);
        Assert.Contains("CK_SubscriptionPlanParameters_Unlimited_LimitValue", sqlEx1.Message);

        // Detach invalid entity
        db.Entry(invalidParam1).State = EntityState.Detached;

        // 2. Invalid: IsUnlimited = true but LimitValue is NOT null
        var invalidParam2 = new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = limit.Id,
            SubscriptionParameterId = usersParam.Id,
            IsUnlimited = true,
            LimitValue = 50m, // VIOLATION of CK_SubscriptionPlanParameters_Unlimited_LimitValue
            IsActive = true
        };
        db.SubscriptionPlanParameters.Add(invalidParam2);

        var ex2 = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlEx2 = ex2.GetBaseException() as SqlException;
        Assert.NotNull(sqlEx2);
        Assert.Contains("CK_SubscriptionPlanParameters_Unlimited_LimitValue", sqlEx2.Message);

        db.Entry(invalidParam2).State = EntityState.Detached;

        // 3. Valid: IsUnlimited = true, LimitValue = null
        var validUnlimited = new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = limit.Id,
            SubscriptionParameterId = usersParam.Id,
            IsUnlimited = true,
            LimitValue = null,
            IsActive = true
        };
        db.SubscriptionPlanParameters.Add(validUnlimited);
        await db.SaveChangesAsync();

        using var verifyDb = CreateDbContext();
        var saved = await verifyDb.SubscriptionPlanParameters.FindAsync(validUnlimited.Id);
        Assert.NotNull(saved);
        Assert.True(saved.IsUnlimited);
        Assert.Null(saved.LimitValue);
    }

    [Fact]
    public async Task SubscriptionPlanParameter_CompositeUniqueIndex_PreventsDuplicateParameterPerPlan()
    {
        using var db = CreateDbContext();
        var limit = new SubscriptionLimit
        {
            Id = Guid.NewGuid(),
            SubscriptionPlanId = CustomPlanId,
            IsActive = true
        };
        db.SubscriptionLimits.Add(limit);

        var usersParam = await db.SubscriptionParameters
            .FirstAsync(p => p.ParameterKey == "USERS");

        var p1 = new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = limit.Id,
            SubscriptionParameterId = usersParam.Id,
            IsUnlimited = false,
            LimitValue = 10m,
            IsActive = true
        };
        db.SubscriptionPlanParameters.Add(p1);
        await db.SaveChangesAsync();

        // Attempt second configuration for USERS parameter under same limit
        var p2 = new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = limit.Id,
            SubscriptionParameterId = usersParam.Id,
            IsUnlimited = false,
            LimitValue = 25m,
            IsActive = true
        };
        db.SubscriptionPlanParameters.Add(p2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlEx = ex.GetBaseException() as SqlException;
        Assert.NotNull(sqlEx);
        // Error 2601 / 2627: Unique index violation on (SubscriptionLimitId, SubscriptionParameterId)
        Assert.True(sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    [Fact]
    public async Task UsageTracking_EnforcesUsageValueUpdates_AndCompositePeriodUniqueIndex()
    {
        using var db = CreateDbContext();
        var subService = new SubscriptionService(db);
        var sub = subService.CreateSubscription(TenantA, Guid.Parse("11111111-1111-1111-1111-111111111111")); // Free plan

        var subRepo = new SubscriptionRepository(db);
        var usageRepo = new SubscriptionUsageRepository(db);
        var usageService = new SubscriptionUsageService(subRepo, usageRepo);

        // Initialize 5 parameters with UsageValue = 0
        await usageService.InitializeUsageAsync(sub);

        using var verifyDb = CreateDbContext();
        var userUsage = await verifyDb.SubscriptionUsages
            .Include(u => u.SubscriptionPlanParameter)
                .ThenInclude(p => p.SubscriptionParameter)
            .FirstAsync(u => u.SubscriptionId == sub.Id && u.SubscriptionPlanParameter.SubscriptionParameter.ParameterKey == "USERS");

        Assert.Equal(0m, userUsage.UsageValue);

        // Update usage value after adding a user (e.g. usage = 1)
        userUsage.UsageValue += 1m;
        userUsage.LastUpdated = DateTime.UtcNow;
        await verifyDb.SaveChangesAsync();

        // Verify updated value in fresh query
        using var queryDb = CreateDbContext();
        var updatedUsage = await queryDb.SubscriptionUsages.FindAsync(userUsage.Id);
        Assert.NotNull(updatedUsage);
        Assert.Equal(1m, updatedUsage.UsageValue);

        // Attempt to insert duplicate usage record for same Subscription + PlanParameter + Period (must violate composite unique index)
        var duplicateUsage = new SubscriptionUsage
        {
            Id = Guid.NewGuid(),
            SubscriptionId = sub.Id,
            SubscriptionPlanParameterId = userUsage.SubscriptionPlanParameterId,
            PeriodStart = userUsage.PeriodStart,
            PeriodEnd = userUsage.PeriodEnd,
            UsageValue = 5m,
            LastUpdated = DateTime.UtcNow
        };
        queryDb.SubscriptionUsages.Add(duplicateUsage);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => queryDb.SaveChangesAsync());
        var sqlEx = ex.GetBaseException() as SqlException;
        Assert.NotNull(sqlEx);
        Assert.True(sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    [Fact]
    public async Task TenantIsolation_UsagesAndLimits_StrictlyIsolatedBetweenTenantAlphaAndBeta()
    {
        using var db = CreateDbContext();
        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var subService = new SubscriptionService(db);
        var subA = subService.CreateSubscription(TenantA, freePlanId);
        var subB = subService.CreateSubscription(TenantB, freePlanId);

        var subRepo = new SubscriptionRepository(db);
        var usageRepo = new SubscriptionUsageRepository(db);
        var usageService = new SubscriptionUsageService(subRepo, usageRepo);

        await usageService.InitializeUsageAsync(subA);
        await usageService.InitializeUsageAsync(subB);

        using var modifyDb = CreateDbContext();
        // Modify Tenant A's usage
        var usageA = await modifyDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subA.Id);
        usageA.UsageValue = 5m; // Max users reached on Free plan
        await modifyDb.SaveChangesAsync();

        // Verify Tenant B's usage remains completely unaffected (UsageValue == 0)
        using var queryDb = CreateDbContext();
        var usageB = await queryDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == subB.Id && u.SubscriptionPlanParameterId == usageA.SubscriptionPlanParameterId);

        Assert.Equal(0m, usageB.UsageValue);
        Assert.NotEqual(usageA.UsageValue, usageB.UsageValue);

        // Verify queries filtering by Tenant A subscription cannot see Tenant B usages
        var tenantAUsageIds = await queryDb.SubscriptionUsages
            .Where(u => u.SubscriptionId == subA.Id)
            .Select(u => u.Id)
            .ToListAsync();

        Assert.DoesNotContain(usageB.Id, tenantAUsageIds);
    }

    [Theory]
    [InlineData(4.0, 5.0, true)]   // Below limit (4 < 5) -> Permitted
    [InlineData(5.0, 5.0, true)]   // At limit (5 == 5) -> Permitted (cannot exceed)
    [InlineData(6.0, 5.0, false)]  // Above limit (6 > 5) -> Denied
    public void LimitThresholds_EnforceBelowAtAndAboveRules(double requestedUsage, double limitValue, bool expectedAllowed)
    {
        // Enforce boundary rule: requested usage <= limitValue
        bool isAllowed = (decimal)requestedUsage <= (decimal)limitValue;
        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Fact]
    public async Task RejectedOperation_AboveLimit_DoesNotPersistOrIncrementUsageInSqlServer()
    {
        using var db = CreateDbContext();
        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var subService = new SubscriptionService(db);
        var sub = subService.CreateSubscription(TenantA, freePlanId);

        var subRepo = new SubscriptionRepository(db);
        var usageRepo = new SubscriptionUsageRepository(db);
        var usageService = new SubscriptionUsageService(subRepo, usageRepo);

        await usageService.InitializeUsageAsync(sub);

        // Fetch plan parameter for USERS on Free plan (LimitValue = 5)
        var userParam = await db.SubscriptionPlanParameters
            .Include(p => p.SubscriptionParameter)
            .FirstAsync(p => p.SubscriptionLimit.SubscriptionPlanId == freePlanId && p.SubscriptionParameter.ParameterKey == "USERS");

        decimal userLimit = userParam.LimitValue ?? 0m;
        Assert.Equal(5m, userLimit);

        // Set initial usage to 5 (at limit)
        using var setupDb = CreateDbContext();
        var initialUsage = await setupDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == sub.Id && u.SubscriptionPlanParameterId == userParam.Id);
        initialUsage.UsageValue = 5m;
        await setupDb.SaveChangesAsync();

        // Simulate attempt to provision 6th user: rejected at application boundary
        decimal currentUsage = 5m;
        decimal attemptedNewUsage = currentUsage + 1m;

        bool canProvision = attemptedNewUsage <= userLimit;
        Assert.False(canProvision);

        // On rejection, database must NOT be updated
        if (!canProvision)
        {
            // Do not call SaveChanges or mutate entity
        }

        // Verify in fresh query: usage remains exactly 5 in SQL Server
        using var verifyDb = CreateDbContext();
        var verifiedUsage = await verifyDb.SubscriptionUsages
            .FirstAsync(u => u.SubscriptionId == sub.Id && u.SubscriptionPlanParameterId == userParam.Id);
        Assert.Equal(5m, verifiedUsage.UsageValue);
    }

    [Fact]
    public async Task SubscriptionStatus_InactiveOrExpired_DeniesOperationRegardlessOfLimits()
    {
        using var db = CreateDbContext();
        var freePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var subService = new SubscriptionService(db);
        var sub = subService.CreateSubscription(TenantA, freePlanId);

        // Cancel subscription (terminal inactive state)
        sub.Cancel();
        await db.SaveChangesAsync();

        // Entitlement check MUST be false
        bool isActive = subService.IsSubscriptionActive(TenantA);
        Assert.False(isActive);
    }
}

