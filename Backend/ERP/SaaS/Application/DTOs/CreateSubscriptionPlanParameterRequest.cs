using SaaS.Core.Models;

using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class CreateSubscriptionPlanParameterRequest
{
    public Guid SubscriptionLimitId { get; set; }

    public Guid SubscriptionParameterId { get; set; }

    public decimal? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }

    public DurationCycle? Duration { get; set; }

    public bool IsActive { get; set; } = true;
}