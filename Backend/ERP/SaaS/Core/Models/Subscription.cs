using SaaS.Common;
using SaaS.Core.Rules;

namespace SaaS.Core.Models;

public class Subscription : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionPlanId { get; set; }

    public string SubscriptionName { get; set; } = string.Empty;

    public string SubscriptionType { get; set; } = string.Empty;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public TenantStorageMode StorageMode { get; set; }
        = TenantStorageMode.Shared;

    public DurationCycle BillingCycle { get; set; } = DurationCycle.Monthly;

    public decimal BillingPlanPriceSnapshot { get; set; }

    public DateTime BillingAnchorDate { get; set; }

    public DateTime CurrentPeriodStart { get; set; }

    public DateTime CurrentPeriodEnd { get; set; }

    public DateTime? NextBillingDate { get; set; }

    public bool AutoRenew { get; set; } = true;

    public DateTime? LastInvoiceGeneratedAt { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    public ICollection<SubscriptionUsage> Usages { get; set; }
        = new List<SubscriptionUsage>();

    public ICollection<SubscriptionInvoice> Invoices { get; set; }
        = new List<SubscriptionInvoice>();

    // ==========================================
    // Domain Lifecycle Transition Methods
    // ==========================================

    public void TransitionTo(SubscriptionStatus targetStatus)
    {
        SubscriptionRules.ValidateTransition(Status, targetStatus);
        Status = targetStatus;
        IsActive = SubscriptionRules.DeriveIsActive(targetStatus);
    }

    public void Activate() => TransitionTo(SubscriptionStatus.Active);

    public void MarkPastDue() => TransitionTo(SubscriptionStatus.PastDue);

    public void Suspend() => TransitionTo(SubscriptionStatus.Suspended);

    public void Expire() => TransitionTo(SubscriptionStatus.Expired);

    public void Cancel() => TransitionTo(SubscriptionStatus.Cancelled);
}