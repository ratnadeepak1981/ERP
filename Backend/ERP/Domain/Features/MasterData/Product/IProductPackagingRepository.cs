using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Product;

public interface IProductPackagingRepository
{
    Task<ProductPackaging?> GetByIdAsync(Guid tenantId, Guid productPackagingId);
    Task<ProductPackaging?> GetByCodeAsync(Guid tenantId, string packagingCode);
    Task<List<ProductPackaging>> GetByProductIdAsync(Guid tenantId, Guid productId, bool includeInactive = false);
    Task<List<ProductPackaging>> GetByProductAndUomAsync(Guid tenantId, Guid productId, Guid packagingUomId);
    Task<List<ProductPackaging>> GetByTenantAsync(Guid tenantId, Guid? productId = null, Guid? companyId = null, bool includeInactive = false);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string packagingCode, Guid? excludeId = null);
    Task AddAsync(ProductPackaging packaging);
    Task SaveChangesAsync();
}
