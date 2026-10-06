using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ITenantDatabaseRepository
{
    Task<TenantDatabase?> GetByTenantIdAsync(Guid tenantId);

    Task<bool> ExistsByTenantIdAsync(Guid tenantId);

    Task AddAsync(TenantDatabase tenantDatabase);

    Task SaveChangesAsync();
}