using SaaS.Common;
using SaaS.Core.Rules;

namespace SaaS.Core.Models;

public class SubscriptionInvoice : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public int SequenceNumber { get; set; } = 1;

    public DurationCycle BillingCycle { get; set; } = DurationCycle.Monthly;

    public DateTime BillingPeriodStart { get; set; }

    public DateTime BillingPeriodEnd { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal CreditedAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime IssueDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Subscription Subscription { get; set; } = null!;

    public ICollection<SubscriptionInvoiceLine> Lines { get; set; }
        = new List<SubscriptionInvoiceLine>();

    public ICollection<PaymentAllocation> PaymentAllocations { get; set; }
        = new List<PaymentAllocation>();

    public ICollection<CreditNote> CreditNotes { get; set; }
        = new List<CreditNote>();

    // Outstanding balance remaining to be paid
    public decimal OutstandingAmount => BillingRules.CalculateOutstandingAmount(TotalAmount, PaidAmount, CreditedAmount);

    public void TransitionTo(InvoiceStatus targetStatus)
    {
        BillingRules.ValidateInvoiceTransition(Status, targetStatus);
        Status = targetStatus;
    }

    public void ApplyPayment(decimal amount, DateTime paidAtUtc)
    {
        BillingRules.ValidateApplyPayment(this, amount);
        PaidAmount += amount;

        if (OutstandingAmount == 0m)
        {
            Status = InvoiceStatus.Paid;
            PaidAt = paidAtUtc;
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
        }
    }

    public void ApplyCredit(decimal amount)
    {
        BillingRules.ValidateApplyCredit(this, amount);
        CreditedAmount += amount;

        if (OutstandingAmount == 0m)
        {
            Status = InvoiceStatus.Paid;
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
        }
    }
}
