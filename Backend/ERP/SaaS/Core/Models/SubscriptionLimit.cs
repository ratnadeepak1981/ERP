using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionLimit : Auditable
{
    public Guid Id { get; set; }

    public Guid SubscriptionPlanId { get; set; }

    public bool IsActive { get; set; } = true;

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    public ICollection<SubscriptionPlanParameter> Parameters { get; set; }
        = new List<SubscriptionPlanParameter>();
}