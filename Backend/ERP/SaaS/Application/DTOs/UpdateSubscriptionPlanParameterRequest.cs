using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class UpdateSubscriptionPlanParameterRequest
{
    public decimal? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }

    public DurationCycle? Duration { get; set; }

    public bool IsActive { get; set; } = true;
}