namespace SaaS.Application.DTOs;

public class CreateSubscriptionLimitRequest
{
    public Guid SubscriptionPlanId { get; set; }

    public bool IsActive { get; set; } = true;
}