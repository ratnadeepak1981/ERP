using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory.StockCount;

public interface IStockCountService
{
    Task<StockCountDto> CreateDraftAsync(Guid tenantId, CreateStockCountDraftRequest request);
    Task<List<StockCountDto>> GetCountsAsync(Guid tenantId, Guid? warehouseId = null, Guid? warehouseLocationId = null, StockCountStatus? status = null);
    Task<StockCountDto?> GetByIdAsync(Guid tenantId, Guid stockCountId);
    Task<StockCountDto> UpdateDraftAsync(Guid tenantId, Guid stockCountId, UpdateStockCountDraftRequest request);
    Task<StockCountDto> SubmitAsync(Guid tenantId, Guid stockCountId, SubmitStockCountRequest request);
    Task<StockCountDto> ApproveAsync(Guid tenantId, Guid stockCountId, ApproveStockCountRequest request);
    Task<StockCountDto> PostAsync(Guid tenantId, Guid stockCountId, PostStockCountRequest request);
    Task<StockCountDto> CancelAsync(Guid tenantId, Guid stockCountId, CancelStockCountRequest request);
}
