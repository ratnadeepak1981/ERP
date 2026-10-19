using SaaS.Common;

namespace SaaS.Core.Models;

public class RefundTransaction : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PaymentTransactionId { get; set; }

    public string ProviderRefundId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string Reason { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; }

    public PaymentTransaction PaymentTransaction { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
