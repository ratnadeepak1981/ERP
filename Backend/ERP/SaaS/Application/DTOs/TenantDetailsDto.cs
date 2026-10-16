using System;

namespace SaaS.Application.DTOs;

public class TenantDetailsDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    // Subscription details
    public Guid? SubscriptionId { get; set; }

    public Guid? SubscriptionPlanId { get; set; }

    public string SubscriptionName { get; set; } = string.Empty;

    public string SubscriptionType { get; set; } = string.Empty;

    public string SubscriptionStatus { get; set; } = "None";

    public string StorageMode { get; set; } = "None";

    public DateTime? SubscriptionStartDate { get; set; }

    public DateTime? SubscriptionEndDate { get; set; }

    // Database & Provisioning details
    public bool IsProvisioned { get; set; }

    public string? DatabaseName { get; set; }

    public string? DatabaseServer { get; set; }

    public bool DatabaseIsActive { get; set; }

    public bool ConfigurationExists { get; set; }
}
