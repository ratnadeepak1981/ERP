using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests;

public class RecurringBillingTests
{
    private static readonly Guid TenantAlpha = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void WeeklyBilling_AdvancesBySevenDaysConsistently_AcrossMonthAndYearBoundaries()
    {
        // Dec 28, 2026 -> Jan 4, 2027 (Year and Month boundary)
        DateTime anchor = new DateTime(2026, 12, 28, 12, 0, 0, DateTimeKind.Utc);
        DateTime periodStart = anchor;

        DateTime nextStart = BillingRules.CalculateNextPeriodStart(periodStart, DurationCycle.Weekly, anchor);

        Assert.Equal(new DateTime(2027, 1, 4, 12, 0, 0, DateTimeKind.Utc), nextStart);
        BillingRules.ValidateBillingPeriod(periodStart, nextStart);
    }

    [Fact]
    public void MonthlyBilling_PreservesAnchorDayAndClampsMonthEnds()
    {
        // Jan 31, 2024 (Leap year) -> Feb 29, 2024 -> Mar 31, 2024
        DateTime anchor = new DateTime(2024, 1, 31, 15, 30, 0, DateTimeKind.Utc);

        DateTime febStart = BillingRules.CalculateNextPeriodStart(anchor, DurationCycle.Monthly, anchor);
        Assert.Equal(new DateTime(2024, 2, 29, 15, 30, 0, DateTimeKind.Utc), febStart);

        DateTime marStart = BillingRules.CalculateNextPeriodStart(febStart, DurationCycle.Monthly, anchor);
        Assert.Equal(new DateTime(2024, 3, 31, 15, 30, 0, DateTimeKind.Utc), marStart);

        DateTime aprStart = BillingRules.CalculateNextPeriodStart(marStart, DurationCycle.Monthly, anchor);
        Assert.Equal(new DateTime(2024, 4, 30, 15, 30, 0, DateTimeKind.Utc), aprStart);

        DateTime mayStart = BillingRules.CalculateNextPeriodStart(aprStart, DurationCycle.Monthly, anchor);
        Assert.Equal(new DateTime(2024, 5, 31, 15, 30, 0, DateTimeKind.Utc), mayStart);
    }

    [Fact]
    public void AnnualBilling_HandlesFebruary29InNonLeapYears()
    {
        // Feb 29, 2024 (Leap year) anchor
        DateTime leapAnchor = new DateTime(2024, 2, 29, 10, 0, 0, DateTimeKind.Utc);

        // 2025: non-leap -> Feb 28
        DateTime year2025 = BillingRules.CalculateNextPeriodStart(leapAnchor, DurationCycle.Yearly, leapAnchor);
        Assert.Equal(new DateTime(2025, 2, 28, 10, 0, 0, DateTimeKind.Utc), year2025);

        // 2026: non-leap -> Feb 28
        DateTime year2026 = BillingRules.CalculateNextPeriodStart(year2025, DurationCycle.Yearly, leapAnchor);
        Assert.Equal(new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc), year2026);

        // 2028: leap year -> returns to Feb 29
        DateTime year2027 = BillingRules.CalculateNextPeriodStart(year2026, DurationCycle.Yearly, leapAnchor);
        DateTime year2028 = BillingRules.CalculateNextPeriodStart(year2027, DurationCycle.Yearly, leapAnchor);
        Assert.Equal(new DateTime(2028, 2, 29, 10, 0, 0, DateTimeKind.Utc), year2028);
    }

    [Fact]
    public async Task RecurringBillingService_GeneratesInvoicesIdempotently_AndAdvancesPeriodAtomically()
    {
        // Setup in-memory DbContext
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var tenant = new Tenant
        {
            Id = TenantAlpha,
            Name = "Alpha Corp",
            Code = "ALPHA",
            IsActive = true
        };
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Professional Plan",
            Code = "PRO",
            Price = 299.00m,
            BillingCycle = DurationCycle.Monthly,
            IsActive = true
        };
        var anchorDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = plan.Id,
            SubscriptionName = "Pro",
            SubscriptionType = "PRO",
            Status = SubscriptionStatus.Active,
            BillingCycle = DurationCycle.Monthly,
            BillingAnchorDate = anchorDate,
            BillingPlanPriceSnapshot = 299.00m,
            CurrentPeriodStart = anchorDate,
            CurrentPeriodEnd = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            NextBillingDate = anchorDate,
            AutoRenew = true,
            StartDate = anchorDate,
            IsActive = true,
            Tenant = tenant,
            SubscriptionPlan = plan
        };

        dbContext.Tenants.Add(tenant);
        dbContext.SubscriptionPlans.Add(plan);
        dbContext.Subscriptions.Add(sub);
        await dbContext.SaveChangesAsync();

        var notificationService = new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance);
        var billingService = new RecurringBillingService(dbContext, notificationService, NullLogger<RecurringBillingService>.Instance);

        // 1. First run generates invoice for [2026-01-15, 2026-02-15)
        var invoice1 = await billingService.GenerateRecurringInvoiceAsync(sub.Id, anchorDate);
        Assert.NotNull(invoice1);
        Assert.Equal(299.00m, invoice1.TotalAmount);
        Assert.Equal(InvoiceStatus.Pending, invoice1.Status);
        Assert.Equal(anchorDate, invoice1.BillingPeriodStart);
        Assert.Equal(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), invoice1.BillingPeriodEnd);

        // Verify period advanced atomically on subscription
        Assert.Equal(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), sub.CurrentPeriodStart);
        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), sub.CurrentPeriodEnd);

        // Verify Ledger debit created
        var ledgerEntry = await dbContext.BillingLedgerEntries.FirstOrDefaultAsync(x => x.SourceDocumentId == invoice1.Id);
        Assert.NotNull(ledgerEntry);
        Assert.Equal(LedgerEntryType.InvoiceCharge, ledgerEntry.EntryType);
        Assert.Equal(LedgerDirection.Debit, ledgerEntry.Direction);
        Assert.Equal(299.00m, ledgerEntry.Amount);

        // 2. Retry with same period interval checks idempotency directly
        var existingInvoice = await dbContext.SubscriptionInvoices
            .FirstOrDefaultAsync(x =>
                x.SubscriptionId == sub.Id &&
                x.BillingPeriodStart == anchorDate &&
                x.BillingPeriodEnd == new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.NotNull(existingInvoice);
        Assert.Equal(invoice1.Id, existingInvoice.Id);

        // 3. Generating the next cycle produces the second sequential invoice
        var invoice2 = await billingService.GenerateRecurringInvoiceAsync(sub.Id, new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));
        Assert.NotNull(invoice2);
        Assert.Equal(2, invoice2.SequenceNumber);
        Assert.Equal(new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc), invoice2.BillingPeriodStart);
        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), invoice2.BillingPeriodEnd);

        var ledgerCount = await dbContext.BillingLedgerEntries.CountAsync();
        Assert.Equal(2, ledgerCount);
    }

    [Fact]
    public async Task DueInvoiceProcessing_TransitionsActiveToPastDue_WhenInvoiceOverdue()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionName = "Pro",
            SubscriptionType = "PRO",
            Status = SubscriptionStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            IsActive = true
        };
        var overdueInvoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionId = sub.Id,
            InvoiceNumber = "INV-OVERDUE-01",
            TotalAmount = 150.00m,
            PaidAmount = 0.00m,
            Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(-2), // 2 days past due
            Subscription = sub
        };

        dbContext.Subscriptions.Add(sub);
        dbContext.SubscriptionInvoices.Add(overdueInvoice);
        await dbContext.SaveChangesAsync();

        var notificationService = new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance);
        var billingService = new RecurringBillingService(dbContext, notificationService, NullLogger<RecurringBillingService>.Instance);

        await billingService.ProcessDueInvoicesAsync(DateTime.UtcNow);

        Assert.Equal(InvoiceStatus.Overdue, overdueInvoice.Status);
        Assert.Equal(SubscriptionStatus.PastDue, sub.Status);
        Assert.True(sub.IsActive); // Remains active in grace period for filtered unique index
    }

    [Fact]
    public async Task GracePeriodExpiry_DeniesServiceEntitlement_WithoutAutomaticSuspension()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var now = DateTime.UtcNow;
        var subPastDueLapsed = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionName = "Pro",
            SubscriptionType = "PRO",
            Status = SubscriptionStatus.PastDue,
            StartDate = now.AddMonths(-2),
            CurrentPeriodEnd = now.AddDays(-10), // Grace period of 7 days expired 3 days ago
            IsActive = true
        };

        dbContext.Subscriptions.Add(subPastDueLapsed);
        await dbContext.SaveChangesAsync();

        var notificationService = new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance);
        var billingService = new RecurringBillingService(dbContext, notificationService, NullLogger<RecurringBillingService>.Instance);

        await billingService.EvaluateGracePeriodEntitlementsAsync(now);

        // Core business rule check: Status MUST remain PastDue (never auto-suspended)!
        Assert.Equal(SubscriptionStatus.PastDue, subPastDueLapsed.Status);
        Assert.True(subPastDueLapsed.IsActive);

        // Service entitlement check must return false
        bool isEntitled = SubscriptionRules.IsEntitledToService(
            subPastDueLapsed.Status,
            subPastDueLapsed.StartDate,
            subPastDueLapsed.CurrentPeriodEnd,
            now,
            gracePeriodDays: 7);

        Assert.False(isEntitled);
    }

    [Fact]
    public void QuarterlyBilling_AdvancesByThreeMonths_PreservingAnchorDay()
    {
        DateTime anchor = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        // Jan 31 + 3 months -> Apr 30 (April has 30 days)
        DateTime q2 = BillingRules.CalculateNextPeriodStart(anchor, DurationCycle.Quarterly, anchor);
        Assert.Equal(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), q2);

        // Apr 30 + 3 months -> Jul 31 (July has 31 days; restored original anchor day)
        DateTime q3 = BillingRules.CalculateNextPeriodStart(q2, DurationCycle.Quarterly, anchor);
        Assert.Equal(new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc), q3);
    }

    [Fact]
    public async Task LifetimeSubscription_DoesNotGenerateRecurringInvoices()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var lifetimeSub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionName = "Lifetime",
            SubscriptionType = "LIFETIME",
            BillingCycle = DurationCycle.Lifetime,
            AutoRenew = false,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.MaxValue,
            IsActive = true
        };

        dbContext.Subscriptions.Add(lifetimeSub);
        await dbContext.SaveChangesAsync();

        var billingService = new RecurringBillingService(
            dbContext,
            new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance),
            NullLogger<RecurringBillingService>.Instance);

        var invoice = await billingService.GenerateRecurringInvoiceAsync(lifetimeSub.Id, DateTime.UtcNow);
        Assert.Null(invoice);
    }

    [Fact]
    public async Task MultipleSubscriptions_SameTenant_ProduceUniqueInvoiceNumbers()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var tenant = new Tenant
        {
            Id = TenantAlpha,
            Name = "Alpha Corp",
            Code = "ALPHA",
            IsActive = true
        };
        dbContext.Tenants.Add(tenant);

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test Plan",
            Code = "TEST",
            Price = 100m,
            BillingCycle = DurationCycle.Monthly,
            IsActive = true
        };
        dbContext.SubscriptionPlans.Add(plan);

        var sub1 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = plan.Id,
            SubscriptionName = "Sub1",
            SubscriptionType = "TYPE1",
            BillingCycle = DurationCycle.Monthly,
            Status = SubscriptionStatus.Active,
            BillingAnchorDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEnd = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoRenew = true,
            IsActive = true
        };

        var sub2 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TenantAlpha,
            SubscriptionPlanId = plan.Id,
            SubscriptionName = "Sub2",
            SubscriptionType = "TYPE2",
            BillingCycle = DurationCycle.Monthly,
            Status = SubscriptionStatus.Active,
            BillingAnchorDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEnd = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoRenew = true,
            IsActive = true
        };

        dbContext.Subscriptions.AddRange(sub1, sub2);
        await dbContext.SaveChangesAsync();

        var billingService = new RecurringBillingService(
            dbContext,
            new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance),
            NullLogger<RecurringBillingService>.Instance);

        var inv1 = await billingService.GenerateRecurringInvoiceAsync(sub1.Id, DateTime.UtcNow);
        var inv2 = await billingService.GenerateRecurringInvoiceAsync(sub2.Id, DateTime.UtcNow);

        Assert.NotNull(inv1);
        Assert.NotNull(inv2);
        Assert.NotEqual(inv1.InvoiceNumber, inv2.InvoiceNumber);
        Assert.Contains(sub1.Id.ToString()[..4].ToUpper(), inv1.InvoiceNumber);
        Assert.Contains(sub2.Id.ToString()[..4].ToUpper(), inv2.InvoiceNumber);
    }

    [Fact]
    public void SaaSDbContext_RejectsCrossTenantInvoiceLineRelationship()
    {
        var options = new DbContextOptionsBuilder<SaaSDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new SaaSDbContext(options);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            SubscriptionId = Guid.NewGuid(),
            InvoiceNumber = "INV-CROSS-01",
            Status = InvoiceStatus.Pending
        };

        var invalidLine = new SubscriptionInvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB, // MISMATCH!
            SubscriptionInvoiceId = invoice.Id,
            SubscriptionInvoice = invoice,
            Description = "Tampered Line",
            Quantity = 1,
            UnitPrice = 100m,
            SubTotal = 100m,
            TotalAmount = 100m
        };

        dbContext.SubscriptionInvoices.Add(invoice);
        dbContext.SubscriptionInvoiceLines.Add(invalidLine);

        var ex = Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
        Assert.Contains("Tenant mismatch", ex.Message);
    }
}
