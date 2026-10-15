using System;

namespace SaaS.Application.DTOs;

public class TenantQueueItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? PlanName { get; set; }

    public string SubscriptionStatus { get; set; } = "None";

    public string StorageMode { get; set; } = "None";

    public string Status { get; set; } = "Pending"; // "Pending" | "Completed"

    public bool IsProvisioned { get; set; }

    public string? DatabaseName { get; set; }
}
