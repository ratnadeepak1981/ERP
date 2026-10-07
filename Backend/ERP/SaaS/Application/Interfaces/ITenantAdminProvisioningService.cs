namespace SaaS.Application.Interfaces;

public interface ITenantAdminProvisioningService
{
    Task EnsureTenantAdminRoleAsync();

    Task EnsureTenantAdminAsync(
        Guid tenantId,
        string username,
        string email,
        string password);
}