using System;
using System.Collections.Generic;
using System.Linq;
using SaaS.Core.Models;

namespace SaaS.Core.Rules;

public static class BillingRules
{
    // ==========================================
    // 1. Transition Rules
    // ==========================================

    private static readonly Dictionary<InvoiceStatus, HashSet<InvoiceStatus>> PermittedInvoiceTransitions = new()
    {
        [InvoiceStatus.Draft] = new() { InvoiceStatus.Pending, InvoiceStatus.Cancelled },
        [InvoiceStatus.Pending] = new() { InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Failed, InvoiceStatus.Cancelled },
        [InvoiceStatus.PartiallyPaid] = new() { InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Cancelled },
        [InvoiceStatus.Overdue] = new() { InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Failed, InvoiceStatus.Cancelled },
        [InvoiceStatus.Failed] = new() { InvoiceStatus.Pending, InvoiceStatus.Cancelled },
        [InvoiceStatus.Paid] = new(), // Terminal - adjustments require CreditNote / Refund
        [InvoiceStatus.Cancelled] = new() // Terminal
    };

    private static readonly Dictionary<PaymentStatus, HashSet<PaymentStatus>> PermittedPaymentTransitions = new()
    {
        [PaymentStatus.Pending] = new() { PaymentStatus.Success, PaymentStatus.Failed },
        [PaymentStatus.Success] = new() { PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded },
        [PaymentStatus.PartiallyRefunded] = new() { PaymentStatus.Refunded },
        [PaymentStatus.Failed] = new(), // Terminal
        [PaymentStatus.Refunded] = new() // Terminal
    };

    public static bool CanTransition(InvoiceStatus current, InvoiceStatus target)
    {
        if (current == target) return true;
        return PermittedInvoiceTransitions.TryGetValue(current, out var allowed) && allowed.Contains(target);
    }

    public static void ValidateInvoiceTransition(InvoiceStatus current, InvoiceStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException($"Invalid invoice transition from '{current}' to '{target}'.");
        }
    }

    public static bool CanTransition(PaymentStatus current, PaymentStatus target)
    {
        if (current == target) return true;
        return PermittedPaymentTransitions.TryGetValue(current, out var allowed) && allowed.Contains(target);
    }

    public static void ValidatePaymentTransition(PaymentStatus current, PaymentStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException($"Invalid payment transition from '{current}' to '{target}'.");
        }
    }

    // ==========================================
    // 2. Financial Calculations & Rules
    // ==========================================

    public static decimal CalculateOutstandingAmount(decimal totalAmount, decimal paidAmount, decimal creditedAmount)
    {
        if (totalAmount < 0m || paidAmount < 0m || creditedAmount < 0m)
        {
            throw new ArgumentOutOfRangeException("Monetary amounts must be non-negative.");
        }

        var balance = totalAmount - (paidAmount + creditedAmount);
        return balance < 0m ? 0m : balance;
    }

    public static decimal CalculateUnallocatedAmount(decimal totalAmount, decimal allocatedAmount, PaymentStatus status)
    {
        if (status != PaymentStatus.Success && status != PaymentStatus.PartiallyRefunded)
        {
            return 0m;
        }

        if (totalAmount < 0m || allocatedAmount < 0m)
        {
            throw new ArgumentOutOfRangeException("Monetary amounts must be non-negative.");
        }

        var unallocated = totalAmount - allocatedAmount;
        return unallocated < 0m ? 0m : unallocated;
    }

    public static decimal CalculateRefundableAmount(decimal totalAmount, decimal refundedAmount, PaymentStatus status)
    {
        if (status != PaymentStatus.Success && status != PaymentStatus.PartiallyRefunded)
        {
            return 0m;
        }

        if (totalAmount < 0m || refundedAmount < 0m)
        {
            throw new ArgumentOutOfRangeException("Monetary amounts must be non-negative.");
        }

        var refundable = totalAmount - refundedAmount;
        return refundable < 0m ? 0m : refundable;
    }

    public static void ValidateApplyPayment(SubscriptionInvoice invoice, decimal paymentAmount)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));

        if (paymentAmount <= 0m)
        {
            throw new InvalidOperationException("Payment allocation amount must be strictly greater than zero.");
        }

        if (invoice.Status == InvoiceStatus.Paid || invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot apply payment to invoice in status '{invoice.Status}'.");
        }

        if (paymentAmount > invoice.OutstandingAmount)
        {
            throw new InvalidOperationException($"Payment amount ({paymentAmount:N2}) exceeds invoice outstanding balance ({invoice.OutstandingAmount:N2}).");
        }
    }

    public static void ValidateApplyCredit(SubscriptionInvoice invoice, decimal creditAmount)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));

        if (creditAmount <= 0m)
        {
            throw new InvalidOperationException("Credit note amount must be strictly greater than zero.");
        }

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot apply credit note to a cancelled invoice.");
        }

        if (creditAmount > invoice.OutstandingAmount)
        {
            throw new InvalidOperationException($"Credit amount ({creditAmount:N2}) exceeds invoice outstanding balance ({invoice.OutstandingAmount:N2}).");
        }
    }

    public static void ValidateAllocation(PaymentTransaction payment, SubscriptionInvoice invoice, decimal amount)
    {
        if (payment == null) throw new ArgumentNullException(nameof(payment));
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));

        if (payment.TenantId != invoice.TenantId)
        {
            throw new InvalidOperationException($"Cross-tenant payment allocation is strictly forbidden. Payment Tenant: {payment.TenantId}, Invoice Tenant: {invoice.TenantId}.");
        }

        if (payment.Status != PaymentStatus.Success && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new InvalidOperationException($"Payment must be in Success status to allocate funds. Current status: '{payment.Status}'.");
        }

        if (amount <= 0m)
        {
            throw new InvalidOperationException("Allocation amount must be greater than zero.");
        }

        if (amount > payment.UnallocatedAmount)
        {
            throw new InvalidOperationException($"Allocation amount ({amount:N2}) exceeds payment unallocated balance ({payment.UnallocatedAmount:N2}).");
        }

        if (amount > invoice.OutstandingAmount)
        {
            throw new InvalidOperationException($"Allocation amount ({amount:N2}) exceeds invoice outstanding balance ({invoice.OutstandingAmount:N2}).");
        }
    }

    public static decimal CalculateTenantReceivableBalance(IEnumerable<BillingLedgerEntry> entries)
    {
        // Receivable Balance = Total Debits (Receivables owed) - Total Credits (Payments/Credits applied)
        decimal balance = 0m;
        foreach (var entry in entries)
        {
            if (entry.Direction == LedgerDirection.Debit)
            {
                balance += entry.Amount;
            }
            else if (entry.Direction == LedgerDirection.Credit)
            {
                balance -= entry.Amount;
            }
        }
        return balance;
    }

    // ==========================================
    // 3. Recurring Billing Date Arithmetic (Half-Open [Start, End))
    // ==========================================

    public static DateTime CalculateNextPeriodStart(
        DateTime currentPeriodStart,
        DurationCycle cycle,
        DateTime billingAnchorDate)
    {
        return cycle switch
        {
            DurationCycle.Weekly => currentPeriodStart.AddDays(7),
            DurationCycle.Monthly => AddMonthsPreservingAnchor(currentPeriodStart, 1, billingAnchorDate),
            DurationCycle.Quarterly => AddMonthsPreservingAnchor(currentPeriodStart, 3, billingAnchorDate),
            DurationCycle.Yearly => AddYearsPreservingAnchor(currentPeriodStart, 1, billingAnchorDate),
            DurationCycle.Lifetime => DateTime.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(cycle), $"Unsupported billing cycle: {cycle}")
        };
    }

    public static DateTime AddMonthsPreservingAnchor(DateTime current, int monthsToAdd, DateTime anchorDate)
    {
        int anchorDay = anchorDate.Day;
        int targetTotalMonths = (current.Year * 12 + (current.Month - 1)) + monthsToAdd;
        int targetYear = targetTotalMonths / 12;
        int targetMonth = (targetTotalMonths % 12) + 1;

        int daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
        int targetDay = Math.Min(anchorDay, daysInMonth);

        return new DateTime(
            targetYear,
            targetMonth,
            targetDay,
            anchorDate.Hour,
            anchorDate.Minute,
            anchorDate.Second,
            DateTimeKind.Utc);
    }

    public static DateTime AddYearsPreservingAnchor(DateTime current, int yearsToAdd, DateTime anchorDate)
    {
        int targetYear = current.Year + yearsToAdd;
        int targetMonth = anchorDate.Month;
        int targetDay = anchorDate.Day;

        // Handle leap year (Feb 29 anchored)
        if (targetMonth == 2 && targetDay == 29 && !DateTime.IsLeapYear(targetYear))
        {
            targetDay = 28;
        }

        return new DateTime(
            targetYear,
            targetMonth,
            targetDay,
            anchorDate.Hour,
            anchorDate.Minute,
            anchorDate.Second,
            DateTimeKind.Utc);
    }

    public static void ValidateBillingPeriod(DateTime periodStart, DateTime periodEnd)
    {
        if (periodEnd <= periodStart)
        {
            throw new InvalidOperationException($"Invalid billing period: End ({periodEnd:O}) must be strictly after Start ({periodStart:O}).");
        }
    }
}
