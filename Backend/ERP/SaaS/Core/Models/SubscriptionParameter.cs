using SaaS.Common;

namespace SaaS.Core.Models;

public class SubscriptionParameter : Auditable
{
    public Guid Id { get; set; }

    public string ParameterKey { get; set; } = null!;

    public SubscriptionParameterType ParameterType { get; set; }
        = SubscriptionParameterType.Fixed;

    public bool IsActive { get; set; } = true;

    public ICollection<SubscriptionPlanParameter> PlanParameters { get; set; }
        = new List<SubscriptionPlanParameter>();
}