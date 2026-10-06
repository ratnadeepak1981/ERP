using SaaS.Application.Interfaces;
using Security.Services;

namespace API.Services;

public class TenantAdminProvisioningService
    : ITenantAdminProvisioningService
{
    private readonly SecurityBootstrapService _securityBootstrapService;

    public TenantAdminProvisioningService(
        SecurityBootstrapService securityBootstrapService)
    {
        _securityBootstrapService = securityBootstrapService;
    }

    public async Task EnsureTenantAdminAsync(
        Guid tenantId,
        string username,
        string email,
        string password)
    {
        await _securityBootstrapService.EnsureTenantAdminAsync(
            tenantId,
            username,
            email,
            password);
    }
}