using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using Xunit;

namespace ERP.SecurityTests;

public class SubscriptionLifecycleTests
{
    [Theory]
    [InlineData(SubscriptionStatus.PendingPayment, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PendingPayment, SubscriptionStatus.Cancelled, true)]
    [InlineData(SubscriptionStatus.PendingPayment, SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.PendingPayment, SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.PendingPayment, SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Suspended, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Cancelled, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PendingPayment, false)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Suspended, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.Cancelled, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.PendingPayment, false)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled, true)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Active, false)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Suspended, false)]
    public void CanTransition_ValidatesAllowedAndRejectedTransitions(
        SubscriptionStatus current,
        SubscriptionStatus target,
        bool expectedAllowed)
    {
        bool allowed = SubscriptionRules.CanTransition(current, target);
        Assert.Equal(expectedAllowed, allowed);

        if (expectedAllowed)
        {
            // Transition validation must not throw
            SubscriptionRules.ValidateTransition(current, target);
        }
        else
        {
            // Transition validation must throw
            Assert.Throws<InvalidOperationException>(() =>
                SubscriptionRules.ValidateTransition(current, target));
        }
    }

    [Fact]
    public void Subscription_DomainMethods_EnforceTransitionRulesAndMaintainIsActiveConsistency()
    {
        // 1. Initial pending commercial subscription
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = SubscriptionStatus.PendingPayment,
            IsActive = false
        };

        // 2. Activate: PendingPayment -> Active
        subscription.Activate();
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.IsActive);

        // 3. PastDue: Active -> PastDue (retains IsActive = true during grace period)
        subscription.MarkPastDue();
        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.True(subscription.IsActive);

        // 4. Suspend: PastDue -> Suspended (IsActive becomes false)
        subscription.Suspend();
        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.False(subscription.IsActive);

        // 5. Reactivate: Suspended -> Active
        subscription.Activate();
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.IsActive);

        // 6. Cancel: Active -> Cancelled (terminal state, IsActive becomes false)
        subscription.Cancel();
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.False(subscription.IsActive);

        // 7. Attempting any transition from Cancelled throws
        Assert.Throws<InvalidOperationException>(() => subscription.Activate());
        Assert.Throws<InvalidOperationException>(() => subscription.MarkPastDue());
        Assert.Throws<InvalidOperationException>(() => subscription.Suspend());
    }

    [Fact]
    public void Subscription_TerminalStateExpired_CannotTransitionOut()
    {
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            IsActive = true
        };

        subscription.Expire();
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
        Assert.False(subscription.IsActive);

        // Cannot transition from Expired
        Assert.Throws<InvalidOperationException>(() => subscription.Activate());
        Assert.Throws<InvalidOperationException>(() => subscription.Cancel());
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.PendingPayment, false)]
    [InlineData(SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    public void DeriveIsActive_MapsStatusCorrectlyForFilteredUniqueIndex(
        SubscriptionStatus status,
        bool expectedIsActive)
    {
        bool isActive = SubscriptionRules.DeriveIsActive(status);
        Assert.Equal(expectedIsActive, isActive);
    }

    [Fact]
    public void IsWithinGracePeriod_EnforcesConfigurableGracePeriodPolicy()
    {
        var now = DateTime.UtcNow;
        var endDate = now.AddDays(-3); // Expired 3 days ago

        // 3 days ago is within default 7-day grace period
        Assert.True(SubscriptionRules.IsWithinGracePeriod(endDate, now, gracePeriodDays: 7));

        // 3 days ago is outside a 2-day grace period
        Assert.False(SubscriptionRules.IsWithinGracePeriod(endDate, now, gracePeriodDays: 2));

        // Exactly on grace period boundary (7 days)
        var boundaryEndDate = now.AddDays(-7);
        Assert.True(SubscriptionRules.IsWithinGracePeriod(boundaryEndDate, now, gracePeriodDays: 7));

        // Past 7 days (8 days ago)
        var pastBoundaryEndDate = now.AddDays(-8);
        Assert.False(SubscriptionRules.IsWithinGracePeriod(pastBoundaryEndDate, now, gracePeriodDays: 7));

        // Null endDate (ongoing/lifetime) is always within grace period
        Assert.True(SubscriptionRules.IsWithinGracePeriod(null, now));
    }

    [Fact]
    public void IsEntitledToService_ValidatesEntitlementAcrossStatusesAndDates()
    {
        var now = DateTime.UtcNow;
        var validStart = now.AddMonths(-1);

        // Active ongoing subscription -> Entitled
        Assert.True(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Active, validStart, null, now));

        // Active within term -> Entitled
        Assert.True(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Active, validStart, now.AddDays(5), now));

        // Active past term -> Not entitled (term has elapsed)
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Active, validStart, now.AddDays(-1), now));

        // PastDue within 7-day grace period -> Entitled
        Assert.True(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.PastDue, validStart, now.AddDays(-3), now, gracePeriodDays: 7));

        // PastDue beyond 7-day grace period -> Not entitled
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.PastDue, validStart, now.AddDays(-8), now, gracePeriodDays: 7));

        // PendingPayment, Suspended, Expired, Cancelled -> Never entitled
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.PendingPayment, validStart, null, now));
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Suspended, validStart, null, now));
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Expired, validStart, now.AddDays(-1), now));
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Cancelled, validStart, null, now));

        // Future start date -> Not entitled even if Active
        Assert.False(SubscriptionRules.IsEntitledToService(
            SubscriptionStatus.Active, now.AddDays(1), null, now));
    }

    [Fact]
    public void Migration_AppliesSuccessfullyToPlatformDbTest_AndVerifiesSchema()
    {
        // 1. Connection string and safety guard verification
        string connStr = TestConstants.PlatformDbTest;
        DatabaseSafetyGuard.AssertSafeTestDatabase(connStr);

        var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SaaS.Infrastructure.Persistence.SaaSDbContext>();
        builder.UseSqlServer(connStr);

        using var db = new SaaS.Infrastructure.Persistence.SaaSDbContext(builder.Options);
        string actualDbName = db.Database.GetDbConnection().Database;

        Assert.Equal("PlatformDB_Test", actualDbName);
        Assert.EndsWith("_Test", actualDbName, StringComparison.OrdinalIgnoreCase);

        // 2. Query Status column via raw SQL against PlatformDB_Test to verify Stage 1 schema
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        db.Database.OpenConnection();
        cmd.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Subscriptions') AND name = 'Status'";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }
}

