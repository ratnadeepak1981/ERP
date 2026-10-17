using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory;

public interface IInventoryRepository
{
    Task<InventoryTransaction?> GetTransactionByIdAsync(Guid tenantId, Guid transactionId);
    Task<List<InventoryTransaction>> GetTransactionsByOriginalIdAsync(Guid tenantId, Guid originalTransactionId);
    Task<bool> TransactionExistsByIdempotencyKeyAsync(
        Guid tenantId,
        SourceDocumentType documentType,
        Guid documentId,
        Guid documentLineId,
        InventoryMovementType movementType,
        int sequence);

    Task AddTransactionAsync(InventoryTransaction transaction);

    Task<InventoryBalance?> GetBalanceAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        string batchNumber,
        string serialNumber);

    Task<List<InventoryBalance>> GetBalancesByProductAsync(Guid tenantId, Guid productId);
    Task<List<InventoryBalance>> GetBalancesByLocationAsync(Guid tenantId, Guid locationId);

    Task AddBalanceAsync(InventoryBalance balance);

    Task AcquireValuationLockAsync(Guid tenantId, Guid productId, Guid warehouseId, int timeoutMilliseconds = 15000);
    Task AcquireTransferValuationLocksAsync(Guid tenantId, Guid productId, Guid sourceWarehouseId, Guid destinationWarehouseId, int timeoutMilliseconds = 15000);

    Task<List<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>> GetActiveCostLayersAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        bool ascending = true,
        string? batchNumber = null,
        string? serialNumber = null);

    Task<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer?> GetCostLayerByTransactionIdAsync(Guid tenantId, Guid transactionId);
    Task AddCostLayerAsync(ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer layer);

    Task<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance?> GetValuationBalanceAsync(Guid tenantId, Guid productId, Guid warehouseId);
    Task AddValuationBalanceAsync(ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance balance);

    Task SaveChangesAsync();
}
