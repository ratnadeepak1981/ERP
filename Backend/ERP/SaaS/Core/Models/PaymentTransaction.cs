using SaaS.Common;
using SaaS.Core.Rules;

namespace SaaS.Core.Models;

public class PaymentTransaction : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string PaymentProvider { get; set; } = string.Empty;

    public string ProviderTransactionId { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Dummy;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public decimal Amount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal RefundedAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime ProcessedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<PaymentAllocation> Allocations { get; set; }
        = new List<PaymentAllocation>();

    public ICollection<RefundTransaction> Refunds { get; set; }
        = new List<RefundTransaction>();

    public decimal UnallocatedAmount => BillingRules.CalculateUnallocatedAmount(Amount, AllocatedAmount, Status);

    public decimal RefundableAmount => BillingRules.CalculateRefundableAmount(Amount, RefundedAmount, Status);

    public void TransitionTo(PaymentStatus targetStatus)
    {
        BillingRules.ValidatePaymentTransition(Status, targetStatus);
        Status = targetStatus;
    }
}
