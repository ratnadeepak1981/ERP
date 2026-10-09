using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaaS.Application.Services;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests;

[Trait("Category", "Integration.Persistence")]
public class SubscriptionLifecyclePersistenceTests : IAsyncLifetime
{
    private static readonly Guid TestTenantId = Guid.Parse("99999999-0000-0000-0000-999999999999");
    private static readonly Guid OtherTenantId = Guid.Parse("88888888-0000-0000-0000-888888888888");
    private static readonly Guid TestPlanId = Guid.Parse("77777777-0000-0000-0000-777777777777");

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

        // Seed dedicated test tenants and test plan
        db.Tenants.Add(new Tenant
        {
            Id = TestTenantId,
            Name = "Persistence Test Tenant",
            Code = "TEST_PERSIST_1",
            IsActive = true
        });

        db.Tenants.Add(new Tenant
        {
            Id = OtherTenantId,
            Name = "Other Isolation Tenant",
            Code = "TEST_PERSIST_2",
            IsActive = true
        });

        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = TestPlanId,
            Name = "Enterprise Cloud",
            Code = "ENT_CLOUD",
            Price = 499.00m,
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

        // Clean dependent rows first in reverse FK order
        var subIds = await db.Subscriptions
            .Where(s => s.TenantId == TestTenantId || s.TenantId == OtherTenantId)
            .Select(s => s.Id)
            .ToListAsync();

        if (subIds.Any())
        {
            var invoiceLines = await db.SubscriptionInvoiceLines
                .Where(l => l.TenantId == TestTenantId || l.TenantId == OtherTenantId)
                .ToListAsync();
            db.SubscriptionInvoiceLines.RemoveRange(invoiceLines);

            var invoices = await db.SubscriptionInvoices
                .Where(i => subIds.Contains(i.SubscriptionId))
                .ToListAsync();
            db.SubscriptionInvoices.RemoveRange(invoices);

            var ledgerEntries = await db.BillingLedgerEntries
                .Where(e => e.TenantId == TestTenantId || e.TenantId == OtherTenantId)
                .ToListAsync();
            db.BillingLedgerEntries.RemoveRange(ledgerEntries);

            var subs = await db.Subscriptions
                .Where(s => subIds.Contains(s.Id))
                .ToListAsync();
            db.Subscriptions.RemoveRange(subs);

            await db.SaveChangesAsync();
        }

        var plan = await db.SubscriptionPlans.FindAsync(TestPlanId);
        if (plan != null) db.SubscriptionPlans.Remove(plan);

        var t1 = await db.Tenants.FindAsync(TestTenantId);
        if (t1 != null) db.Tenants.Remove(t1);

        var t2 = await db.Tenants.FindAsync(OtherTenantId);
        if (t2 != null) db.Tenants.Remove(t2);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PersistSubscription_PendingPaymentState_StoresIsActiveFalseCorrectly()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Enterprise Trial",
            SubscriptionType = "ENT",
            Status = SubscriptionStatus.PendingPayment,
            IsActive = false,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = true,
            StartDate = anchor
        };

        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        // Reload fresh from database
        using var queryDb = CreateDbContext();
        var reloaded = await queryDb.Subscriptions.FindAsync(sub.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(SubscriptionStatus.PendingPayment, reloaded.Status);
        Assert.False(reloaded.IsActive);
        Assert.Equal(499.00m, reloaded.BillingPlanPriceSnapshot);
    }

    [Fact]
    public async Task FilteredUniqueIndex_RejectsSecondActiveSubscription_ForSameTenant()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var sub1 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "First Active",
            SubscriptionType = "ACTIVE_1",
            Status = SubscriptionStatus.Active,
            IsActive = true,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = true,
            StartDate = anchor
        };

        db.Subscriptions.Add(sub1);
        await db.SaveChangesAsync();

        // Attempt to insert second subscription for the SAME tenant with IsActive = true
        var sub2 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Second Active Violation",
            SubscriptionType = "ACTIVE_2",
            Status = SubscriptionStatus.Active,
            IsActive = true,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = true,
            StartDate = anchor
        };

        db.Subscriptions.Add(sub2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sqlEx = ex.GetBaseException() as SqlException;
        Assert.NotNull(sqlEx);
        // SQL Server error 2601 / 2627: Unique index violation on IX_Subscriptions_TenantId
        Assert.Contains("IX_Subscriptions_TenantId", ex.GetBaseException().Message);
    }

    [Fact]
    public async Task FilteredUniqueIndex_AllowsMultipleInactiveSubscriptions_ForSameTenant()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var inactiveSub1 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Cancelled Old Sub",
            SubscriptionType = "INACTIVE_1",
            Status = SubscriptionStatus.Cancelled,
            IsActive = false,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = false,
            StartDate = anchor
        };

        var inactiveSub2 = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Expired Old Sub",
            SubscriptionType = "INACTIVE_2",
            Status = SubscriptionStatus.Expired,
            IsActive = false,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = false,
            StartDate = anchor
        };

        db.Subscriptions.AddRange(inactiveSub1, inactiveSub2);
        await db.SaveChangesAsync();

        // Verify both inactive records exist side-by-side in SQL Server
        using var queryDb = CreateDbContext();
        var count = await queryDb.Subscriptions
            .CountAsync(s => s.TenantId == TestTenantId && !s.IsActive);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task FullLifecycleTransitions_PersistAccurately_AndSynchronizeIsActiveInSqlServer()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Initial PendingPayment (IsActive = false)
        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Lifecycle Subscription",
            SubscriptionType = "LIFECYCLE",
            Status = SubscriptionStatus.PendingPayment,
            IsActive = false,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = true,
            StartDate = anchor
        };

        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        // 2. Transition: Activate (Active -> IsActive = true)
        sub.Activate();
        await db.SaveChangesAsync();

        using (var q1 = CreateDbContext())
        {
            var s = await q1.Subscriptions.FindAsync(sub.Id);
            Assert.NotNull(s);
            Assert.Equal(SubscriptionStatus.Active, s.Status);
            Assert.True(s.IsActive);
        }

        // 3. Transition: MarkPastDue (Active -> PastDue, IsActive remains true during grace period)
        sub.MarkPastDue();
        await db.SaveChangesAsync();

        using (var q2 = CreateDbContext())
        {
            var s = await q2.Subscriptions.FindAsync(sub.Id);
            Assert.NotNull(s);
            Assert.Equal(SubscriptionStatus.PastDue, s.Status);
            Assert.True(s.IsActive);
        }

        // 4. Transition: Suspend (PastDue -> Suspended, IsActive becomes false)
        sub.Suspend();
        await db.SaveChangesAsync();

        using (var q3 = CreateDbContext())
        {
            var s = await q3.Subscriptions.FindAsync(sub.Id);
            Assert.NotNull(s);
            Assert.Equal(SubscriptionStatus.Suspended, s.Status);
            Assert.False(s.IsActive);
        }

        // 5. Transition: Reactivate (Suspended -> Active, IsActive becomes true)
        sub.Activate();
        await db.SaveChangesAsync();

        using (var q4 = CreateDbContext())
        {
            var s = await q4.Subscriptions.FindAsync(sub.Id);
            Assert.NotNull(s);
            Assert.Equal(SubscriptionStatus.Active, s.Status);
            Assert.True(s.IsActive);
        }

        // 6. Transition: Cancel (Active -> Cancelled, terminal state, IsActive becomes false)
        sub.Cancel();
        await db.SaveChangesAsync();

        using (var q5 = CreateDbContext())
        {
            var s = await q5.Subscriptions.FindAsync(sub.Id);
            Assert.NotNull(s);
            Assert.Equal(SubscriptionStatus.Cancelled, s.Status);
            Assert.False(s.IsActive);
        }
    }

    [Fact]
    public async Task RecurringBillingService_ExecutesAgainstPlatformDbTest_PersistingInvoicesAndLedger()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var sub = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionPlanId = TestPlanId,
            SubscriptionName = "Billed Subscription",
            SubscriptionType = "BILLED",
            Status = SubscriptionStatus.Active,
            IsActive = true,
            BillingCycle = DurationCycle.Monthly,
            BillingPlanPriceSnapshot = 499.00m,
            BillingAnchorDate = anchor,
            CurrentPeriodStart = anchor,
            CurrentPeriodEnd = anchor.AddMonths(1),
            AutoRenew = true,
            StartDate = anchor
        };

        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var notificationService = new DummyBillingNotificationService(NullLogger<DummyBillingNotificationService>.Instance);
        var billingService = new RecurringBillingService(db, notificationService, NullLogger<RecurringBillingService>.Instance);

        // Act: Generate recurring invoice for current period
        var invoice = await billingService.GenerateRecurringInvoiceAsync(sub.Id, anchor);

        // Assert: Invoice returned and period atomically advanced
        Assert.NotNull(invoice);
        Assert.Equal(499.00m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
        Assert.Equal(anchor, invoice.BillingPeriodStart);
        Assert.Equal(anchor.AddMonths(1), invoice.BillingPeriodEnd);

        // Verify in fresh DB context that rows exist in SQL Server
        using var verifyDb = CreateDbContext();
        var persistedInvoice = await verifyDb.SubscriptionInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);

        Assert.NotNull(persistedInvoice);
        Assert.Equal(499.00m, persistedInvoice.TotalAmount);
        Assert.Single(persistedInvoice.Lines);

        // Verify BillingLedgerEntry exists in SQL Server
        var ledgerEntry = await verifyDb.BillingLedgerEntries
            .FirstOrDefaultAsync(l => l.SourceDocumentId == invoice.Id);

        Assert.NotNull(ledgerEntry);
        Assert.Equal(LedgerEntryType.InvoiceCharge, ledgerEntry.EntryType);
        Assert.Equal(LedgerDirection.Debit, ledgerEntry.Direction);
        Assert.Equal(499.00m, ledgerEntry.Amount);
        Assert.Equal(TestTenantId, ledgerEntry.TenantId);

        // Verify idempotency against the database for the original period
        var totalInvoicesForFirstPeriod = await verifyDb.SubscriptionInvoices
            .CountAsync(i => i.SubscriptionId == sub.Id && i.BillingPeriodStart == anchor);
        Assert.Equal(1, totalInvoicesForFirstPeriod);

        // Generating for the second period advances sequentially to sequence number 2
        var secondCycleDate = anchor.AddMonths(1);
        var invoice2 = await billingService.GenerateRecurringInvoiceAsync(sub.Id, secondCycleDate);
        Assert.NotNull(invoice2);
        Assert.Equal(2, invoice2.SequenceNumber);
        Assert.Equal(secondCycleDate, invoice2.BillingPeriodStart);
        Assert.Equal(secondCycleDate.AddMonths(1), invoice2.BillingPeriodEnd);

        var totalInvoices = await verifyDb.SubscriptionInvoices
            .CountAsync(i => i.SubscriptionId == sub.Id);
        Assert.Equal(2, totalInvoices);
    }

    [Fact]
    public async Task SaaSDbContext_EnforcesCrossTenantForeignKeyIsolation_InSqlServer()
    {
        using var db = CreateDbContext();
        var anchor = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            SubscriptionId = Guid.NewGuid(),
            InvoiceNumber = "INV-ISOLATION-01",
            Status = InvoiceStatus.Pending,
            BillingPeriodStart = anchor,
            BillingPeriodEnd = anchor.AddMonths(1)
        };

        // Line belongs to OtherTenantId while invoice belongs to TestTenantId
        var invalidLine = new SubscriptionInvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = OtherTenantId,
            SubscriptionInvoiceId = invoice.Id,
            SubscriptionInvoice = invoice,
            Description = "Tampered cross-tenant line",
            Quantity = 1,
            UnitPrice = 100m,
            SubTotal = 100m,
            TotalAmount = 100m
        };

        db.SubscriptionInvoices.Add(invoice);
        db.SubscriptionInvoiceLines.Add(invalidLine);

        // SaaSDbContext domain interceptor and SQL constraints reject cross-tenant mismatch
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("Tenant mismatch", ex.Message);
    }
}
