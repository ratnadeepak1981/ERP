using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ITenantConfigurationRepository
{
    Task<TenantConfiguration?> GetByTenantIdAsync(Guid tenantId);

    Task<bool> ExistsByTenantIdAsync(Guid tenantId);

    Task AddAsync(TenantConfiguration configuration);

    Task SaveChangesAsync();
}