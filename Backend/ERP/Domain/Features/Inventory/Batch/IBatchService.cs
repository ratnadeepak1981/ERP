using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory.Batch;

public interface IBatchService
{
    Task<List<ProductBatchDto>> GetBatchesAsync(Guid tenantId, Guid? productId = null);
    Task<ProductBatchDto?> GetBatchByIdAsync(Guid tenantId, Guid productBatchId);
    Task<ProductBatchDto?> GetBatchByNumberAsync(Guid tenantId, Guid productId, string batchNumber);
    Task<ProductBatchDto> CreateBatchAsync(Guid tenantId, CreateProductBatchRequest request);
    Task<List<EligibleBatchDto>> GetEligibleBatchesAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId = null,
        Guid? locationId = null,
        DateTime? asOfDate = null);
}
