using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;
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

    public async Task<ERP.Domain.Features.Inventory.Batch.ProductBatch?> GetBatchAsync(Guid tenantId, Guid productId, string batchNumber)
    {
        string normBatch = batchNumber?.Trim() ?? string.Empty;
        return await _context.Set<ERP.Domain.Features.Inventory.Batch.ProductBatch>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProductId == productId && x.BatchNumber == normBatch);
    }

    public async Task<ERP.Domain.Features.Inventory.Batch.ProductBatch?> GetBatchByNumberAsync(Guid tenantId, string batchNumber)
    {
        string normBatch = batchNumber?.Trim() ?? string.Empty;
        return await _context.Set<ERP.Domain.Features.Inventory.Batch.ProductBatch>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BatchNumber == normBatch);
    }

    public async Task<List<ERP.Domain.Features.Inventory.Batch.ProductBatch>> GetBatchesByProductAsync(Guid tenantId, Guid productId)
    {
        return await _context.Set<ERP.Domain.Features.Inventory.Batch.ProductBatch>()
            .Where(x => x.TenantId == tenantId && x.ProductId == productId)
            .OrderBy(x => x.BatchNumber)
            .ToListAsync();
    }

    public async Task AddBatchAsync(ERP.Domain.Features.Inventory.Batch.ProductBatch batch)
    {
        await _context.Set<ERP.Domain.Features.Inventory.Batch.ProductBatch>().AddAsync(batch);
    }

    public async Task<List<EligibleBatchDto>> GetEligibleBatchesAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId = null,
        Guid? locationId = null,
        DateTime? asOfDate = null)
    {
        var date = (asOfDate ?? DateTime.UtcNow).Date;

        var balancesQuery = _context.Set<InventoryBalance>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                        && b.ProductId == productId
                        && b.BatchNumber != string.Empty
                        && (b.QuantityOnHand - b.QuantityAllocated - b.QuantityQuarantine) > 0);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            balancesQuery = balancesQuery.Where(b => b.WarehouseId == warehouseId.Value);
        }

        if (locationId.HasValue && locationId.Value != Guid.Empty)
        {
            balancesQuery = balancesQuery.Where(b => b.WarehouseLocationId == locationId.Value);
        }

        var balances = await balancesQuery.ToListAsync();
        if (balances.Count == 0)
        {
            return new List<EligibleBatchDto>();
        }

        var locationIds = balances.Select(b => b.WarehouseLocationId).Distinct().ToList();
        var locations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && locationIds.Contains(l.WarehouseLocationId) && l.IsActive)
            .ToListAsync();

        var locationTypeIds = locations.Where(l => l.LocationTypeId.HasValue).Select(l => l.LocationTypeId!.Value).Distinct().ToList();
        var locationTypes = locationTypeIds.Count > 0
            ? await _context.LocationTypes
                .AsNoTracking()
                .Where(lt => locationTypeIds.Contains(lt.LocationTypeId))
                .ToDictionaryAsync(lt => lt.LocationTypeId)
            : new Dictionary<Guid, LocationType>();

        var activePickableLocations = locations
            .Where(l => !l.LocationTypeId.HasValue || (locationTypes.TryGetValue(l.LocationTypeId.Value, out var lt) && lt.CanPick && lt.IsActive))
            .ToDictionary(l => l.WarehouseLocationId);

        var warehouseIds = balances.Select(b => b.WarehouseId).Distinct().ToList();
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && warehouseIds.Contains(w.WarehouseId) && w.IsActive)
            .ToDictionaryAsync(w => w.WarehouseId);

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId);

        var batchNumbers = balances.Select(b => b.BatchNumber).Distinct().ToList();
        var batches = await _context.ProductBatches
            .AsNoTracking()
            .Where(pb => pb.TenantId == tenantId && pb.ProductId == productId && batchNumbers.Contains(pb.BatchNumber) && pb.IsActive)
            .ToDictionaryAsync(pb => pb.BatchNumber);

        var result = new List<EligibleBatchDto>();

        foreach (var b in balances)
        {
            if (!activePickableLocations.TryGetValue(b.WarehouseLocationId, out var loc))
                continue;

            if (!warehouses.TryGetValue(b.WarehouseId, out var wh))
                continue;

            if (!batches.TryGetValue(b.BatchNumber, out var pb))
                continue;

            // Exclude expired batches
            if (pb.ExpiryDate.HasValue && pb.ExpiryDate.Value.Date < date)
                continue;

            decimal available = b.QuantityOnHand - b.QuantityAllocated - b.QuantityQuarantine;
            if (available <= 0)
                continue;

            result.Add(new EligibleBatchDto
            {
                ProductId = productId,
                ProductCode = product?.ProductCode ?? string.Empty,
                ProductName = product?.ProductName ?? string.Empty,
                BatchNumber = b.BatchNumber,
                WarehouseId = wh.WarehouseId,
                WarehouseCode = wh.WarehouseCode,
                WarehouseName = wh.WarehouseName,
                WarehouseLocationId = loc.WarehouseLocationId,
                LocationCode = loc.LocationCode,
                LocationName = loc.LocationName,
                QuantityOnHand = b.QuantityOnHand,
                QuantityAvailable = available,
                ManufacturingDate = pb.ManufacturingDate,
                ExpiryDate = pb.ExpiryDate
            });
        }

        // FEFO ordering: earliest expiry first (dated batches before undated), then manufacturing date, then batch number
        return result
            .OrderBy(x => x.ExpiryDate == null ? 1 : 0)
            .ThenBy(x => x.ExpiryDate)
            .ThenBy(x => x.ManufacturingDate)
            .ThenBy(x => x.BatchNumber)
            .ToList();
    }

    public async Task<decimal> GetTotalAvailableStockAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        bool isBatchTracked,
        DateTime? asOfDate = null)
    {
        if (!isBatchTracked)
        {
            var bal = await GetBalanceAsync(tenantId, locationId, productId, string.Empty, string.Empty);
            return bal?.QuantityAvailable ?? 0m;
        }

        var eligible = await GetEligibleBatchesAsync(tenantId, productId, null, locationId, asOfDate);
        return eligible.Sum(x => x.QuantityAvailable);
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
