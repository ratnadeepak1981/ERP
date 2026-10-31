using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Brand;

public interface IBrandService
{
    Task<List<Brand>> GetBrandsAsync(Guid tenantId);
    Task<Brand?> GetBrandAsync(Guid tenantId, Guid brandId);
    Task<Brand> CreateBrandAsync(Guid tenantId, CreateBrandRequest request);
    Task<Brand> UpdateBrandAsync(Guid tenantId, Guid brandId, UpdateBrandRequest request);
    Task DeactivateBrandAsync(Guid tenantId, Guid brandId);
    Task<Brand> ReactivateBrandAsync(Guid tenantId, Guid brandId);
}
