using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class SubscriptionPlanParameterResponse
{
    public Guid Id { get; set; }

    public Guid SubscriptionLimitId { get; set; }

    public Guid SubscriptionParameterId { get; set; }

    public decimal? LimitValue { get; set; }

    public bool IsUnlimited { get; set; }

    public DurationCycle? Duration { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public Guid? ModifiedBy { get; set; }
}