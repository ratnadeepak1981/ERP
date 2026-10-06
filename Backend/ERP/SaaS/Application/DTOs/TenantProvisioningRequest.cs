namespace SaaS.Application.DTOs;

public class TenantProvisioningRequest
{
    public Guid TenantId { get; set; }

    public string AdminUserName { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}