using SaaS.Common;

namespace SaaS.Core.Models;

public class CreditNote : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionInvoiceId { get; set; }

    public string CreditNoteNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Reason { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public SubscriptionInvoice SubscriptionInvoice { get; set; } = null!;
}
