using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionUsage : Auditable
{
    public Guid Id { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid SubscriptionPlanParameterId { get; set; }

    public decimal UsageValue { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public DateTime LastUpdated { get; set; }

    public Subscription Subscription { get; set; } = null!;

    public SubscriptionPlanParameter SubscriptionPlanParameter { get; set; } = null!;
}