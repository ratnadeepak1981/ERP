using SaaS.Common;

namespace SaaS.Core.Models;

public class BillingLedgerEntry : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public LedgerEntryType EntryType { get; set; }

    public LedgerDirection Direction { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime PostedAtUtc { get; set; }

    public string SourceDocumentType { get; set; } = string.Empty; // e.g. "SubscriptionInvoice", "PaymentTransaction", "CreditNote", "RefundTransaction"

    public Guid SourceDocumentId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = null!;
}
