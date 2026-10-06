namespace SaaS.Application.DTOs;

public class SubscriptionLimitResponse
{
    public Guid Id { get; set; }

    public Guid SubscriptionPlanId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public Guid? ModifiedBy { get; set; }
}