using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionPlan : Auditable
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public DurationCycle BillingCycle { get; set; }
        = DurationCycle.Monthly;

    public bool IsActive { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; }
        = new List<Subscription>();

    public SubscriptionLimit? SubscriptionLimit { get; set; }
}