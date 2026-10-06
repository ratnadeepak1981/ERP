using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class UpdateSubscriptionPlanRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public DurationCycle BillingCycle { get; set; }

    public bool IsActive { get; set; }
}