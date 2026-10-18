using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionPlanParameter : Auditable
{
    public Guid Id { get; set; }

    public Guid SubscriptionLimitId { get; set; }

    public Guid SubscriptionParameterId { get; set; }

    public decimal? LimitValue { get; set; }

    public bool IsUnlimited { get; set; } = false;

    public DurationCycle? Duration { get; set; }

    public bool IsActive { get; set; } = true;

    public SubscriptionLimit SubscriptionLimit { get; set; } = null!;

    public SubscriptionParameter SubscriptionParameter { get; set; } = null!;

    public ICollection<SubscriptionUsage> Usages { get; set; }
        = new List<SubscriptionUsage>();
}