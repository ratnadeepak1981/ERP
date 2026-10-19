using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory;

public class InventoryRepository : IInventoryRepository
{
    private readonly DomainDbContext _context;

    public InventoryRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryTransaction?> GetTransactionByIdAsync(Guid tenantId, Guid transactionId)
    {
        return await _context.Set<InventoryTransaction>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.InventoryTransactionId == transactionId);
    }

    public async Task<List<InventoryTransaction>> GetTransactionsByOriginalIdAsync(Guid tenantId, Guid originalTransactionId)
    {
        return await _context.Set<InventoryTransaction>()
            .Where(x => x.TenantId == tenantId && x.ReversalOfTransactionId == originalTransactionId)
            .ToListAsync();
    }

    public async Task<bool> TransactionExistsByIdempotencyKeyAsync(
        Guid tenantId,
        SourceDocumentType documentType,
        Guid documentId,
        Guid documentLineId,
        InventoryMovementType movementType,
        int sequence)
    {
        return await _context.Set<InventoryTransaction>()
            .AnyAsync(x => x.TenantId == tenantId
                           && x.SourceDocumentType == documentType
                           && x.SourceDocumentId == documentId
                           && x.SourceDocumentLineId == documentLineId
                           && x.MovementType == movementType
                           && x.MovementSequence == sequence);
    }

    public async Task AddTransactionAsync(InventoryTransaction transaction)
    {
        await _context.Set<InventoryTransaction>().AddAsync(transaction);
    }

    public async Task<InventoryBalance?> GetBalanceAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        string batchNumber,
        string serialNumber)
    {
        string normBatch = batchNumber?.Trim() ?? string.Empty;
        string normSerial = serialNumber?.Trim() ?? string.Empty;

        return await _context.Set<InventoryBalance>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId
                                      && x.WarehouseLocationId == locationId
                                      && x.ProductId == productId
                                      && x.BatchNumber == normBatch
                                      && x.SerialNumber == normSerial);
    }

    public async Task<List<InventoryBalance>> GetBalancesByProductAsync(Guid tenantId, Guid productId)
    {
        return await _context.Set<InventoryBalance>()
            .Where(x => x.TenantId == tenantId && x.ProductId == productId)
            .ToListAsync();
    }

    public async Task<List<InventoryBalance>> GetBalancesByLocationAsync(Guid tenantId, Guid locationId)
    {
        return await _context.Set<InventoryBalance>()
            .Where(x => x.TenantId == tenantId && x.WarehouseLocationId == locationId)
            .ToListAsync();
    }

    public async Task AddBalanceAsync(InventoryBalance balance)
    {
        await _context.Set<InventoryBalance>().AddAsync(balance);
    }

    public async Task AcquireValuationLockAsync(Guid tenantId, Guid productId, Guid warehouseId, int timeoutMilliseconds = 15000)
    {
        string resourceName = $"Valuation:{tenantId}:{productId}:{warehouseId}";
        var paramResource = new Microsoft.Data.SqlClient.SqlParameter("@Resource", resourceName);
        var paramTimeout = new Microsoft.Data.SqlClient.SqlParameter("@LockTimeout", timeoutMilliseconds);
        var paramResult = new Microsoft.Data.SqlClient.SqlParameter("@Result", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };

        await _context.Database.ExecuteSqlRawAsync(
            "EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @LockTimeout;",
            paramResult, paramResource, paramTimeout);

        int resultCode = paramResult.Value is int val ? val : -999;
        if (resultCode < 0)
        {
            throw new InvalidOperationException(
                $"Failed to acquire transactional valuation lock for resource '{resourceName}'. Lock return code: {resultCode}. Transaction aborted.");
        }
    }

    public async Task AcquireTransferValuationLocksAsync(Guid tenantId, Guid productId, Guid sourceWarehouseId, Guid destinationWarehouseId, int timeoutMilliseconds = 15000)
    {
        if (sourceWarehouseId == destinationWarehouseId)
        {
            await AcquireValuationLockAsync(tenantId, productId, sourceWarehouseId, timeoutMilliseconds);
            return;
        }

        // Deterministic lock ordering: Compare Guids to always lock in the same sequence
        Guid firstWarehouse = sourceWarehouseId.CompareTo(destinationWarehouseId) < 0 ? sourceWarehouseId : destinationWarehouseId;
        Guid secondWarehouse = sourceWarehouseId.CompareTo(destinationWarehouseId) < 0 ? destinationWarehouseId : sourceWarehouseId;

        await AcquireValuationLockAsync(tenantId, productId, firstWarehouse, timeoutMilliseconds);
        await AcquireValuationLockAsync(tenantId, productId, secondWarehouse, timeoutMilliseconds);
    }

    public async Task<List<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>> GetActiveCostLayersAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        bool ascending = true,
        string? batchNumber = null,
        string? serialNumber = null)
    {
        var query = _context.Set<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>()
            .Where(x => x.TenantId == tenantId
                        && x.ProductId == productId
                        && x.WarehouseId == warehouseId
                        && !x.IsExhausted
                        && x.RemainingQuantity > 0);

        if (!string.IsNullOrWhiteSpace(batchNumber))
        {
            string normBatch = batchNumber.Trim();
            query = query.Where(x => x.BatchNumber == normBatch);
        }

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            string normSerial = serialNumber.Trim();
            query = query.Where(x => x.SerialNumber == normSerial);
        }

        if (ascending)
        {
            return await query
                .OrderBy(x => x.LayerDate)
                .ThenBy(x => x.CostLayerId)
                .ToListAsync();
        }
        else
        {
            return await query
                .OrderByDescending(x => x.LayerDate)
                .ThenByDescending(x => x.CostLayerId)
                .ToListAsync();
        }
    }

    public async Task<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer?> GetCostLayerByTransactionIdAsync(Guid tenantId, Guid transactionId)
    {
        return await _context.Set<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.InventoryTransactionId == transactionId);
    }

    public async Task AddCostLayerAsync(ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer layer)
    {
        await _context.Set<ERP.Domain.Features.Inventory.Valuation.InventoryCostLayer>().AddAsync(layer);
    }

    public async Task<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance?> GetValuationBalanceAsync(Guid tenantId, Guid productId, Guid warehouseId)
    {
        return await _context.Set<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProductId == productId && x.WarehouseId == warehouseId);
    }

    public async Task AddValuationBalanceAsync(ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance balance)
    {
        await _context.Set<ERP.Domain.Features.Inventory.Valuation.ProductValuationBalance>().AddAsync(balance);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
