using SaaS.Application.DTOs;

namespace SaaS.Application.Interfaces;

public interface ITenantProvisioningService
{
    Task<TenantProvisioningResult> ProvisionTenant(
        TenantProvisioningRequest request);
}   