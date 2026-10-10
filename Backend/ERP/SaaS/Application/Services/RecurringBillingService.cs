using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Application.Services;

public class RecurringBillingService : IRecurringBillingService
{
    private readonly SaaSDbContext _dbContext;
    private readonly IBillingNotificationService _notificationService;
    private readonly ILogger<RecurringBillingService> _logger;

    public RecurringBillingService(
        SaaSDbContext dbContext,
        IBillingNotificationService notificationService,
        ILogger<RecurringBillingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<SubscriptionInvoice?> GenerateRecurringInvoiceAsync(
        Guid subscriptionId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _dbContext.Subscriptions
            .Include(x => x.SubscriptionPlan)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken);

        if (subscription == null)
        {
            _logger.LogWarning("Subscription {SubscriptionId} not found.", subscriptionId);
            return null;
        }

        // Do not generate recurring invoices for Lifetime plans, inactive plans, or cancelled/expired subscriptions
        if (subscription.BillingCycle == DurationCycle.Lifetime ||
            !subscription.AutoRenew ||
            subscription.Status == SubscriptionStatus.Cancelled ||
            subscription.Status == SubscriptionStatus.Expired)
        {
            _logger.LogInformation("Subscription {SubscriptionId} is not eligible for recurring invoice generation. Status: {Status}, Cycle: {Cycle}",
                subscriptionId, subscription.Status, subscription.BillingCycle);
            return null;
        }

        DateTime periodStart = subscription.CurrentPeriodStart;
        DateTime periodEnd = subscription.CurrentPeriodEnd;

        BillingRules.ValidateBillingPeriod(periodStart, periodEnd);

        // Check if invoice for this period interval already exists (Idempotent outcome)
        var existingInvoice = await _dbContext.SubscriptionInvoices
            .FirstOrDefaultAsync(x =>
                x.SubscriptionId == subscriptionId &&
                x.BillingPeriodStart == periodStart &&
                x.BillingPeriodEnd == periodEnd,
                cancellationToken);

        if (existingInvoice != null)
        {
            _logger.LogInformation("Invoice already generated for subscription {SubscriptionId} and period [{Start}, {End}). Returning existing invoice {InvoiceNumber}.",
                subscriptionId, periodStart, periodEnd, existingInvoice.InvoiceNumber);
            return existingInvoice;
        }

        // Determine sequence number
        int sequenceNumber = await _dbContext.SubscriptionInvoices
            .CountAsync(x => x.SubscriptionId == subscriptionId, cancellationToken) + 1;

        // Deterministic unique invoice number supporting weekly, monthly, and yearly cycles
        string cycleCode = subscription.BillingCycle switch
        {
            DurationCycle.Weekly => "WK",
            DurationCycle.Monthly => "MO",
            DurationCycle.Quarterly => "QTR",
            DurationCycle.Yearly => "YR",
            _ => "GEN"
        };
        string subPrefix = subscription.Id.ToString()[..4].ToUpper();
        string invoiceNumber = $"INV-{subscription.TenantId.ToString()[..6].ToUpper()}-{subPrefix}-{cycleCode}-{periodStart:yyyyMMdd}-{sequenceNumber:D3}";

        decimal planPrice = subscription.BillingPlanPriceSnapshot > 0m
            ? subscription.BillingPlanPriceSnapshot
            : subscription.SubscriptionPlan?.Price ?? 0m;

        DateTime dueDate = periodStart.AddDays(7); // Standard 7-day payment window

        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            SubscriptionId = subscription.Id,
            InvoiceNumber = invoiceNumber,
            SequenceNumber = sequenceNumber,
            BillingCycle = subscription.BillingCycle,
            BillingPeriodStart = periodStart,
            BillingPeriodEnd = periodEnd,
            Status = InvoiceStatus.Pending,
            SubTotal = planPrice,
            TaxAmount = 0.00m,
            TotalAmount = planPrice,
            PaidAmount = 0.00m,
            CreditedAmount = 0.00m,
            Currency = "USD",
            IssueDate = asOfUtc,
            DueDate = dueDate,
            Notes = $"Recurring {subscription.BillingCycle} billing for period [{periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd})"
        };

        var line = new SubscriptionInvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            SubscriptionInvoiceId = invoice.Id,
            Description = $"{subscription.SubscriptionName} ({subscription.BillingCycle})",
            Quantity = 1,
            UnitPrice = planPrice,
            SubTotal = planPrice,
            TaxRate = 0.0000m,
            TaxAmount = 0.00m,
            TotalAmount = planPrice
        };
        invoice.Lines.Add(line);

        // Financial Ledger entry: Debit charge for tenant receivable
        var ledgerEntry = new BillingLedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            EntryType = LedgerEntryType.InvoiceCharge,
            Direction = LedgerDirection.Debit,
            Amount = planPrice,
            Currency = "USD",
            PostedAtUtc = asOfUtc,
            SourceDocumentType = nameof(SubscriptionInvoice),
            SourceDocumentId = invoice.Id,
            ReferenceNumber = invoiceNumber,
            Description = $"Invoice charge for {subscription.BillingCycle} subscription period"
        };

        // Advance subscription period atomically
        DateTime nextStart = periodEnd;
        DateTime nextEnd = BillingRules.CalculateNextPeriodStart(nextStart, subscription.BillingCycle, subscription.BillingAnchorDate);

        subscription.CurrentPeriodStart = nextStart;
        subscription.CurrentPeriodEnd = nextEnd;
        subscription.NextBillingDate = nextStart;
        subscription.LastInvoiceGeneratedAt = asOfUtc;

        await _dbContext.SubscriptionInvoices.AddAsync(invoice, cancellationToken);
        await _dbContext.BillingLedgerEntries.AddAsync(ledgerEntry, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully generated recurring invoice {InvoiceNumber} for subscription {SubscriptionId}.", invoiceNumber, subscriptionId);
        }
        catch (DbUpdateException ex)
        {
            // Handle concurrent worker idempotency safely: if unique index on (SubscriptionId, BillingPeriodStart, BillingPeriodEnd) was hit
            _logger.LogWarning(ex, "Unique constraint hit during invoice generation for subscription {SubscriptionId}. Checking for concurrent creation.", subscriptionId);
            
            var concurrentInvoice = await _dbContext.SubscriptionInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.SubscriptionId == subscriptionId &&
                    x.BillingPeriodStart == periodStart &&
                    x.BillingPeriodEnd == periodEnd,
                    cancellationToken);

            if (concurrentInvoice != null)
            {
                return concurrentInvoice;
            }
            throw;
        }

        return invoice;
    }

    public async Task ProcessDueInvoicesAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        // Find unpaid invoices past due date
        var overdueInvoices = await _dbContext.SubscriptionInvoices
            .Include(x => x.Subscription)
            .Where(x =>
                (x.Status == InvoiceStatus.Pending || x.Status == InvoiceStatus.PartiallyPaid) &&
                x.DueDate < asOfUtc)
            .ToListAsync(cancellationToken);

        foreach (var invoice in overdueInvoices)
        {
            invoice.Status = InvoiceStatus.Overdue;

            // Mark subscription as PastDue per lifecycle rules if it's currently Active
            if (invoice.Subscription != null && invoice.Subscription.Status == SubscriptionStatus.Active)
            {
                invoice.Subscription.MarkPastDue();
                _logger.LogWarning("Subscription {SubscriptionId} transitioned to PastDue due to overdue invoice {InvoiceNumber}.",
                    invoice.SubscriptionId, invoice.InvoiceNumber);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EvaluateGracePeriodEntitlementsAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        // Grace-period evaluation: Evaluates service entitlement.
        // Rule: NEVER automatically transition a subscription to Suspended!
        // Grace-period expiry merely denies service entitlement via SubscriptionRules.IsEntitledToService(...)
        var pastDueSubscriptions = await _dbContext.Subscriptions
            .Where(x => x.Status == SubscriptionStatus.PastDue)
            .ToListAsync(cancellationToken);

        foreach (var sub in pastDueSubscriptions)
        {
            bool isEntitled = SubscriptionRules.IsEntitledToService(
                sub.Status,
                sub.StartDate,
                sub.CurrentPeriodEnd,
                asOfUtc,
                SubscriptionRules.DefaultGracePeriodDays);

            if (!isEntitled)
            {
                _logger.LogInformation("Subscription {SubscriptionId} has exhausted its grace period. Service entitlement is denied. (Status remains {Status} awaiting manual admin action).",
                    sub.Id, sub.Status);
            }
        }
    }

    public async Task SendBillingNotificationsAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        // 1. Reminders for invoices due in <= 3 days
        var upcomingInvoices = await _dbContext.SubscriptionInvoices
            .Where(x =>
                x.Status == InvoiceStatus.Pending &&
                x.DueDate >= asOfUtc &&
                x.DueDate <= asOfUtc.AddDays(3))
            .ToListAsync(cancellationToken);

        foreach (var inv in upcomingInvoices)
        {
            await _notificationService.SendInvoiceDueReminderAsync(
                inv.TenantId, inv.Id, inv.InvoiceNumber, inv.OutstandingAmount, inv.DueDate, cancellationToken);
        }

        // 2. Warnings for PastDue subscriptions in grace period
        var pastDueSubs = await _dbContext.Subscriptions
            .Where(x => x.Status == SubscriptionStatus.PastDue)
            .ToListAsync(cancellationToken);

        foreach (var sub in pastDueSubs)
        {
            DateTime graceEnd = sub.CurrentPeriodEnd.AddDays(SubscriptionRules.DefaultGracePeriodDays);
            if (asOfUtc < graceEnd)
            {
                await _notificationService.SendGracePeriodWarningAsync(
                    sub.TenantId, sub.Id, graceEnd, cancellationToken);
            }
        }
    }
}
