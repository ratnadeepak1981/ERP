using System;

namespace SaaS.Application.DTOs;

public class TenantLifecycleStatusDto
{
    public Guid TenantId { get; set; }

    public bool IsRegistered { get; set; }

    public bool HasActiveSubscription { get; set; }

    public string SubscriptionStatus { get; set; } = "None";

    public string StorageMode { get; set; } = "None";

    public bool DatabaseAllocated { get; set; }

    public string? DatabaseName { get; set; }

    public string? DatabaseServer { get; set; }

    public bool ConfigurationAllocated { get; set; }

    public bool IsProvisioned { get; set; }

    public string OverallStatus { get; set; } = "Pending"; // "Pending" | "Completed"
}
