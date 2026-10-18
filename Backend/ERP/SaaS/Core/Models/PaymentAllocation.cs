using SaaS.Common;

namespace SaaS.Core.Models;

public class PaymentAllocation : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PaymentTransactionId { get; set; }

    public Guid SubscriptionInvoiceId { get; set; }

    public decimal Amount { get; set; }

    public DateTime AllocatedAtUtc { get; set; }

    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    public SubscriptionInvoice SubscriptionInvoice { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
