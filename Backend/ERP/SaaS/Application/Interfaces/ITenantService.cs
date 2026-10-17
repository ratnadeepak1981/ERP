using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ITenantService
{
    Tenant CreateTenant(string name, string code);

    Task<TenantRegistrationResult> RegisterTenant(
        CreateTenantRequest request
        );

    Task<List<TenantQueueItemDto>> GetTenantListAsync();

    Task<TenantDetailsDto?> GetTenantDetailsAsync(Guid tenantId);

    Task<TenantLifecycleStatusDto?> GetTenantStatusAsync(Guid tenantId);

    Task<TenantMetricsDto> GetTenantMetricsAsync();
}