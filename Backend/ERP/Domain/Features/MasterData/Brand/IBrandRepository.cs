using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Brand;

public interface IBrandRepository
{
    Task<List<Brand>> GetByTenantAsync(Guid tenantId);
    Task<Brand?> GetByIdAsync(Guid tenantId, Guid brandId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string brandCode);
    Task<bool> ExistsByNameAsync(Guid tenantId, string brandName);
    Task AddAsync(Brand brand);
    Task SaveChangesAsync();
}
