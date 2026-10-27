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

    Task<ERP.Domain.Features.Inventory.Batch.ProductBatch?> GetBatchAsync(Guid tenantId, Guid productId, string batchNumber);
    Task<ERP.Domain.Features.Inventory.Batch.ProductBatch?> GetBatchByNumberAsync(Guid tenantId, string batchNumber);
    Task<List<ERP.Domain.Features.Inventory.Batch.ProductBatch>> GetBatchesByProductAsync(Guid tenantId, Guid productId);
    Task AddBatchAsync(ERP.Domain.Features.Inventory.Batch.ProductBatch batch);

    Task<List<ERP.Domain.Features.Inventory.Batch.EligibleBatchDto>> GetEligibleBatchesAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId = null,
        Guid? locationId = null,
        DateTime? asOfDate = null);

    Task<decimal> GetTotalAvailableStockAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        bool isBatchTracked,
        DateTime? asOfDate = null);

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
