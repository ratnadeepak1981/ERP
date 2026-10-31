using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Product;

public interface IProductPackagingService
{
    Task<ProductPackagingDto> CreatePackagingAsync(Guid tenantId, CreateProductPackagingRequest request);
    Task<ProductPackagingDto> UpdatePackagingAsync(Guid tenantId, Guid productPackagingId, UpdateProductPackagingRequest request);
    Task DeactivatePackagingAsync(Guid tenantId, Guid productPackagingId);
    Task<ProductPackagingDto> ReactivatePackagingAsync(Guid tenantId, Guid productPackagingId);
    Task<ProductPackagingDto?> GetPackagingByIdAsync(Guid tenantId, Guid productPackagingId);
    Task<List<ProductPackagingDto>> GetPackagingsByProductAsync(Guid tenantId, Guid productId, bool includeInactive = false);
    Task<List<ProductPackagingDto>> GetPackagingsAsync(Guid tenantId, Guid? productId = null, Guid? companyId = null, bool includeInactive = false);
}
