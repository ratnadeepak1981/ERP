namespace SaaS.Application.DTOs;

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;

    public Guid SubscriptionPlanId { get; set; }

    public string? Subdomain { get; set; }

    public string? IndustryType { get; set; }

    public string? CompanyStructure { get; set; } = "Single";

    public bool ECommerceEnabled { get; set; }

    public string? AdminFullName { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPhone { get; set; }
}
