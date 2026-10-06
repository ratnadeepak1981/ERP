namespace SaaS.Application.DTOs;

public class TenantProvisioningResult
{
    public Guid TenantId { get; set; }

    public bool DatabaseProvisioned { get; set; }

    public bool ConfigurationProvisioned { get; set; }

    public bool IsProvisioned { get; set; }

    public string StorageMode { get; set; } = string.Empty;
}