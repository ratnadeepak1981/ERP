using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Supplier;

public interface ISupplierProductPriceService
{
    Task<List<SupplierProductPrice>> GetPricesAsync(Guid tenantId, Guid? supplierId = null, Guid? productId = null);
    Task<SupplierProductPrice?> GetPriceAsync(Guid tenantId, Guid priceId);
    Task<SupplierProductPrice> CreatePriceAsync(Guid tenantId, CreateSupplierProductPriceRequest request);
    Task<SupplierProductPrice> UpdatePriceAsync(Guid tenantId, Guid priceId, UpdateSupplierProductPriceRequest request);
    Task DeactivatePriceAsync(Guid tenantId, Guid priceId);
    Task<SupplierProductPrice?> GetActivePriceAsync(Guid tenantId, Guid supplierId, Guid productId, DateTime? asOfDate = null);
}
