using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Supplier;

public interface ISupplierProductPriceRepository
{
    Task<List<SupplierProductPrice>> GetPricesAsync(Guid tenantId, Guid? supplierId = null, Guid? productId = null);
    Task<SupplierProductPrice?> GetPriceByIdAsync(Guid tenantId, Guid priceId);
    Task<SupplierProductPrice?> GetActivePriceAsync(Guid tenantId, Guid supplierId, Guid productId, DateTime asOfDate);
    Task<List<SupplierProductPrice>> GetPreferredPricesAsync(Guid tenantId, Guid productId);
    Task AddPriceAsync(SupplierProductPrice price);
    Task<bool> SupplierExistsAsync(Guid tenantId, Guid supplierId);
    Task<bool> ProductExistsAsync(Guid tenantId, Guid productId);
    Task<bool> UnitOfMeasureExistsAsync(Guid tenantId, Guid unitOfMeasureId);
    Task SaveChangesAsync();
}
