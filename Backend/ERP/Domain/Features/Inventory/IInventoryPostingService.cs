using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory;

public interface IInventoryPostingService
{
    Task<InventoryTransaction> PostMovementAsync(Guid tenantId, PostInventoryMovementRequest request);
    Task<(InventoryTransaction Outflow, InventoryTransaction Inflow)> PostTransferAsync(Guid tenantId, PostTransferMovementRequest request);
    Task<InventoryTransaction> PostReversalAsync(Guid tenantId, PostReversalRequest request);
    Task<InventoryBalance?> GetBalanceAsync(Guid tenantId, Guid locationId, Guid productId, string? batchNumber = null, string? serialNumber = null);
    Task<List<InventoryBalance>> GetProductBalancesAsync(Guid tenantId, Guid productId);
}
