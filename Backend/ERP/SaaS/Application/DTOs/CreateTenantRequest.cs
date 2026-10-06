namespace SaaS.Application.DTOs;

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    public Guid SubscriptionPlanId { get; set; }
}