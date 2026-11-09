using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Inventory.Putaway;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Inventory;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Infrastructure.Persistence;

public class InventoryPersistenceCoordinator : IInventoryPersistenceCoordinator
{
    private readonly DomainDbContext _context;

    public InventoryPersistenceCoordinator(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public bool HasActiveTransaction => _context.Database.CurrentTransaction != null;

    public async Task<IAsyncDisposable?> BeginTransactionIfNeededAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null; // participating in outer transaction
        }

        return await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx)
    {
        if (tx is IDbContextTransaction dbTx)
        {
            await dbTx.CommitAsync();
            await dbTx.DisposeAsync();
        }
    }

    public async Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx)
    {
        if (tx is IDbContextTransaction dbTx)
        {
            try
            {
                await dbTx.RollbackAsync();
            }
            finally
            {
                await dbTx.DisposeAsync();
            }
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task ExecuteStrategyAsync(Func<Task> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(operation);
    }

    public async Task<T> ExecuteStrategyAsync<T>(Func<Task<T>> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(operation);
    }

    public async Task<InventorySummaryData> GetSummaryDataAsync(Guid tenantId)
    {
        var totalStockItems = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked)
            .CountAsync();

        var totalWarehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .CountAsync();

        var totalLocations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(wl => wl.TenantId == tenantId && wl.IsActive)
            .CountAsync();

        var totalTransactions = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .CountAsync();

        var totalOnHand = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .SumAsync(b => (decimal?)b.QuantityOnHand) ?? 0m;

        var totalValuation = await _context.ProductValuationBalances
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .SumAsync(v => (decimal?)v.TotalCostValue) ?? 0m;

        var productsWithReorder = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked && p.ReorderLevel > 0)
            .Select(p => new { p.ProductId, p.ReorderLevel })
            .ToListAsync();

        var balanceSums = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, TotalOnHand = g.Sum(x => x.QuantityOnHand) })
            .ToListAsync();

        var balanceDict = balanceSums.ToDictionary(b => b.ProductId, b => b.TotalOnHand);

        var lowStockCount = productsWithReorder.Count(p =>
        {
            var onHand = balanceDict.TryGetValue(p.ProductId, out var qty) ? qty : 0m;
            return onHand <= p.ReorderLevel;
        });

        return new InventorySummaryData
        {
            TotalStockItems = totalStockItems,
            TotalWarehouses = totalWarehouses,
            TotalLocations = totalLocations,
            LowStockItems = lowStockCount,
            TotalTransactions = totalTransactions,
            TotalOnHandQuantity = totalOnHand,
            TotalInventoryValue = Math.Round(totalValuation, 2, MidpointRounding.AwayFromZero)
        };
    }

    public async Task<List<InventoryRecentTransactionDto>> GetRecentTransactionsAsync(Guid tenantId, int limit)
    {
        var safeLimit = Math.Max(1, Math.Min(limit, 100));

        var transactions = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(safeLimit)
            .ToListAsync();

        if (transactions.Count == 0)
        {
            return new List<InventoryRecentTransactionDto>();
        }

        var productIds = transactions.Select(t => t.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.UnitOfMeasure })
            .ToDictionaryAsync(p => p.ProductId);

        var warehouseIds = transactions.Select(t => t.WarehouseId).Distinct().ToList();
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .Select(w => new { w.WarehouseId, w.WarehouseCode, w.WarehouseName })
            .ToDictionaryAsync(w => w.WarehouseId);

        var locationIds = transactions.Select(t => t.WarehouseLocationId).Distinct().ToList();
        var locations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.WarehouseLocationId))
            .Select(l => new { l.WarehouseLocationId, l.LocationCode, l.LocationName })
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        return transactions.Select(t =>
        {
            products.TryGetValue(t.ProductId, out var product);
            warehouses.TryGetValue(t.WarehouseId, out var warehouse);
            locations.TryGetValue(t.WarehouseLocationId, out var location);

            return new InventoryRecentTransactionDto
            {
                InventoryTransactionId = t.InventoryTransactionId,
                TransactionNumber = t.TransactionNumber,
                MovementType = t.MovementType.ToString(),
                MovementTypeCode = (int)t.MovementType,
                SourceDocumentType = t.SourceDocumentType.ToString(),
                SourceDocumentId = t.SourceDocumentId,
                ProductId = t.ProductId,
                ProductCode = product?.ProductCode ?? string.Empty,
                ProductName = product?.ProductName ?? "Unknown Product",
                UnitOfMeasure = product?.UnitOfMeasure ?? "PCS",
                WarehouseId = t.WarehouseId,
                WarehouseCode = warehouse?.WarehouseCode ?? string.Empty,
                WarehouseName = warehouse?.WarehouseName ?? "Unknown Warehouse",
                WarehouseLocationId = t.WarehouseLocationId,
                LocationCode = location?.LocationCode ?? string.Empty,
                LocationName = location?.LocationName ?? string.Empty,
                Quantity = t.Quantity,
                UnitCost = t.UnitCost,
                TotalCost = t.TotalCost,
                BatchNumber = t.BatchNumber,
                SerialNumber = t.SerialNumber,
                TransactionDate = t.TransactionDate,
                CreatedAt = t.CreatedAt
            };
        }).ToList();
    }

    public async Task<List<StockIssueDto>> GetStockIssuesAsync(Guid tenantId, Guid? warehouseId, StockIssuePurpose? purpose)
    {
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Quantity < 0);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(t => t.WarehouseId == warehouseId.Value);
        }

        if (purpose.HasValue)
        {
            query = purpose.Value switch
            {
                StockIssuePurpose.SalesDispatch => query.Where(t => t.MovementType == InventoryMovementType.ShipmentDispatch),
                StockIssuePurpose.Manufacturing => query.Where(t => t.MovementType == InventoryMovementType.ProductionIssue),
                StockIssuePurpose.InternalUse => query.Where(t => t.MovementType == InventoryMovementType.StockAdjustmentOut && t.SourceDocumentType == SourceDocumentType.StockAdjustment),
                StockIssuePurpose.Other => query.Where(t => t.MovementType == InventoryMovementType.StockAdjustmentOut && t.SourceDocumentType == SourceDocumentType.ManualPosting),
                _ => query
            };
        }

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync();

        if (transactions.Count == 0)
        {
            return new List<StockIssueDto>();
        }

        var productIds = transactions.Select(t => t.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.UnitOfMeasure })
            .ToDictionaryAsync(p => p.ProductId);

        var warehouseIds = transactions.Select(t => t.WarehouseId).Distinct().ToList();
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .Select(w => new { w.WarehouseId, w.WarehouseCode, w.WarehouseName })
            .ToDictionaryAsync(w => w.WarehouseId);

        var locationIds = transactions.Select(t => t.WarehouseLocationId).Distinct().ToList();
        var locations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.WarehouseLocationId))
            .Select(l => new { l.WarehouseLocationId, l.LocationCode, l.LocationName })
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        return transactions.Select(t =>
        {
            products.TryGetValue(t.ProductId, out var product);
            warehouses.TryGetValue(t.WarehouseId, out var warehouse);
            locations.TryGetValue(t.WarehouseLocationId, out var location);

            var issuePurpose = MapMovementTypeToPurpose(t.MovementType, t.SourceDocumentType);

            return new StockIssueDto
            {
                StockIssueId = t.InventoryTransactionId,
                StockIssueLineId = t.SourceDocumentLineId != Guid.Empty ? t.SourceDocumentLineId : null,
                IssueNumber = t.TransactionNumber,
                IssueDate = t.TransactionDate,
                Purpose = issuePurpose,
                PurposeName = GetPurposeName(issuePurpose),
                MovementType = t.MovementType.ToString(),
                Status = StockIssueStatus.Posted,
                StatusName = "Posted",
                WarehouseId = t.WarehouseId,
                WarehouseCode = warehouse?.WarehouseCode ?? string.Empty,
                WarehouseName = warehouse?.WarehouseName ?? "Unknown Warehouse",
                WarehouseLocationId = t.WarehouseLocationId,
                LocationCode = location?.LocationCode ?? string.Empty,
                LocationName = location?.LocationName ?? string.Empty,
                ProductId = t.ProductId,
                ProductCode = product?.ProductCode ?? string.Empty,
                ProductName = product?.ProductName ?? "Unknown Product",
                UnitOfMeasure = product?.UnitOfMeasure ?? "PCS",
                Quantity = Math.Abs(t.Quantity),
                UnitCost = t.UnitCost,
                TotalCost = t.TotalCost,
                BatchNumber = t.BatchNumber,
                SerialNumber = t.SerialNumber,
                SourceDocumentReference = t.SourceDocumentId != Guid.Empty ? t.SourceDocumentId.ToString() : null,
                CreatedAt = t.CreatedAt
            };
        }).ToList();
    }

    public async Task<StockIssueDto?> GetStockIssueByIdAsync(Guid tenantId, Guid issueId)
    {
        var tx = await _context.InventoryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && (t.InventoryTransactionId == issueId || t.SourceDocumentId == issueId));

        if (tx == null) return null;

        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == tx.ProductId);
        var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.WarehouseId == tx.WarehouseId);
        var location = await _context.WarehouseLocations.AsNoTracking().FirstOrDefaultAsync(l => l.WarehouseLocationId == tx.WarehouseLocationId);

        var purpose = MapMovementTypeToPurpose(tx.MovementType, tx.SourceDocumentType);

        return new StockIssueDto
        {
            StockIssueId = tx.InventoryTransactionId,
            StockIssueLineId = tx.SourceDocumentLineId != Guid.Empty ? tx.SourceDocumentLineId : null,
            IssueNumber = tx.TransactionNumber,
            IssueDate = tx.TransactionDate,
            Purpose = purpose,
            PurposeName = GetPurposeName(purpose),
            MovementType = tx.MovementType.ToString(),
            Status = StockIssueStatus.Posted,
            StatusName = "Posted",
            WarehouseId = tx.WarehouseId,
            WarehouseCode = warehouse?.WarehouseCode ?? string.Empty,
            WarehouseName = warehouse?.WarehouseName ?? "Unknown Warehouse",
            WarehouseLocationId = tx.WarehouseLocationId,
            LocationCode = location?.LocationCode ?? string.Empty,
            LocationName = location?.LocationName ?? string.Empty,
            ProductId = tx.ProductId,
            ProductCode = product?.ProductCode ?? string.Empty,
            ProductName = product?.ProductName ?? "Unknown Product",
            UnitOfMeasure = product?.UnitOfMeasure ?? "PCS",
            Quantity = Math.Abs(tx.Quantity),
            UnitCost = tx.UnitCost,
            TotalCost = tx.TotalCost,
            BatchNumber = tx.BatchNumber,
            SerialNumber = tx.SerialNumber,
            SourceDocumentReference = tx.SourceDocumentId != Guid.Empty ? tx.SourceDocumentId.ToString() : null,
            CreatedAt = tx.CreatedAt
        };
    }

    public async Task<List<StockReceiptDto>> GetStockReceiptsAsync(Guid tenantId, Guid? warehouseId, StockReceiptReason? reason)
    {
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Quantity > 0);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(t => t.WarehouseId == warehouseId.Value);
        }

        if (reason.HasValue)
        {
            query = reason.Value switch
            {
                StockReceiptReason.InternalProduction => query.Where(t => t.MovementType == InventoryMovementType.ProductionReceipt),
                _ => query.Where(t => t.MovementType == InventoryMovementType.StockAdjustmentIn)
            };
        }

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync();

        if (transactions.Count == 0)
        {
            return new List<StockReceiptDto>();
        }

        var productIds = transactions.Select(t => t.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.UnitOfMeasure })
            .ToDictionaryAsync(p => p.ProductId);

        var warehouseIds = transactions.Select(t => t.WarehouseId).Distinct().ToList();
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .Select(w => new { w.WarehouseId, w.WarehouseCode, w.WarehouseName })
            .ToDictionaryAsync(w => w.WarehouseId);

        var locationIds = transactions.Select(t => t.WarehouseLocationId).Distinct().ToList();
        var locations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => locationIds.Contains(l.WarehouseLocationId))
            .Select(l => new { l.WarehouseLocationId, l.LocationCode, l.LocationName })
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        var batchNumbers = transactions
            .Where(t => !string.IsNullOrEmpty(t.BatchNumber))
            .Select(t => t.BatchNumber!)
            .Distinct()
            .ToList();

        var batches = await _context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && productIds.Contains(b.ProductId) && batchNumbers.Contains(b.BatchNumber))
            .Select(b => new { b.ProductId, b.BatchNumber, b.ManufacturingDate, b.ExpiryDate })
            .ToListAsync();

        var batchLookup = batches.ToDictionary(b => (b.ProductId, b.BatchNumber));

        return transactions.Select(t =>
        {
            products.TryGetValue(t.ProductId, out var product);
            warehouses.TryGetValue(t.WarehouseId, out var warehouse);
            locations.TryGetValue(t.WarehouseLocationId, out var location);

            DateTime? mfgDate = null;
            DateTime? expDate = t.ExpiryDate;
            if (!string.IsNullOrEmpty(t.BatchNumber) && batchLookup.TryGetValue((t.ProductId, t.BatchNumber), out var bInfo))
            {
                mfgDate = bInfo.ManufacturingDate;
                expDate ??= bInfo.ExpiryDate;
            }

            var receiptReason = MapMovementTypeToReceiptReason(t.MovementType);

            return new StockReceiptDto
            {
                StockReceiptId = t.InventoryTransactionId,
                StockReceiptLineId = t.SourceDocumentLineId != Guid.Empty ? t.SourceDocumentLineId : null,
                ReceiptNumber = t.TransactionNumber,
                ReceiptDate = t.TransactionDate,
                Reason = receiptReason,
                ReasonName = GetReceiptReasonName(receiptReason),
                MovementType = t.MovementType.ToString(),
                Status = StockReceiptStatus.Posted,
                StatusName = "Posted",
                WarehouseId = t.WarehouseId,
                WarehouseCode = warehouse?.WarehouseCode ?? string.Empty,
                WarehouseName = warehouse?.WarehouseName ?? "Unknown Warehouse",
                WarehouseLocationId = t.WarehouseLocationId,
                LocationCode = location?.LocationCode ?? string.Empty,
                LocationName = location?.LocationName ?? string.Empty,
                ProductId = t.ProductId,
                ProductCode = product?.ProductCode ?? string.Empty,
                ProductName = product?.ProductName ?? "Unknown Product",
                UnitOfMeasure = product?.UnitOfMeasure ?? "PCS",
                Quantity = t.Quantity,
                UnitCost = t.UnitCost,
                TotalCost = t.TotalCost,
                BatchNumber = t.BatchNumber,
                SerialNumber = t.SerialNumber,
                ManufacturingDate = mfgDate,
                ExpiryDate = expDate,
                SourceDocumentReference = t.SourceDocumentId != Guid.Empty ? t.SourceDocumentId.ToString() : null,
                CreatedAt = t.CreatedAt
            };
        }).ToList();
    }

    public async Task<StockReceiptDto?> GetStockReceiptByIdAsync(Guid tenantId, Guid receiptId)
    {
        var t = await _context.InventoryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.InventoryTransactionId == receiptId && x.Quantity > 0);

        if (t == null) return null;

        var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == t.ProductId);
        var warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.WarehouseId == t.WarehouseId);
        var location = await _context.WarehouseLocations.AsNoTracking().FirstOrDefaultAsync(l => l.WarehouseLocationId == t.WarehouseLocationId);

        DateTime? mfgDate = null;
        DateTime? expDate = t.ExpiryDate;
        if (!string.IsNullOrEmpty(t.BatchNumber))
        {
            var bInfo = await _context.ProductBatches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == t.ProductId && b.BatchNumber == t.BatchNumber);
            if (bInfo != null)
            {
                mfgDate = bInfo.ManufacturingDate;
                expDate ??= bInfo.ExpiryDate;
            }
        }

        var receiptReason = MapMovementTypeToReceiptReason(t.MovementType);

        return new StockReceiptDto
        {
            StockReceiptId = t.InventoryTransactionId,
            StockReceiptLineId = t.SourceDocumentLineId != Guid.Empty ? t.SourceDocumentLineId : null,
            ReceiptNumber = t.TransactionNumber,
            ReceiptDate = t.TransactionDate,
            Reason = receiptReason,
            ReasonName = GetReceiptReasonName(receiptReason),
            MovementType = t.MovementType.ToString(),
            Status = StockReceiptStatus.Posted,
            StatusName = "Posted",
            WarehouseId = t.WarehouseId,
            WarehouseCode = warehouse?.WarehouseCode ?? string.Empty,
            WarehouseName = warehouse?.WarehouseName ?? "Unknown Warehouse",
            WarehouseLocationId = t.WarehouseLocationId,
            LocationCode = location?.LocationCode ?? string.Empty,
            LocationName = location?.LocationName ?? string.Empty,
            ProductId = t.ProductId,
            ProductCode = product?.ProductCode ?? string.Empty,
            ProductName = product?.ProductName ?? "Unknown Product",
            UnitOfMeasure = product?.UnitOfMeasure ?? "PCS",
            Quantity = t.Quantity,
            UnitCost = t.UnitCost,
            TotalCost = t.TotalCost,
            BatchNumber = t.BatchNumber,
            SerialNumber = t.SerialNumber,
            ManufacturingDate = mfgDate,
            ExpiryDate = expDate,
            SourceDocumentReference = t.SourceDocumentId != Guid.Empty ? t.SourceDocumentId.ToString() : null,
            CreatedAt = t.CreatedAt
        };
    }

    public async Task<Dictionary<Guid, Product>> GetActiveProductsByIdsAsync(Guid tenantId, IEnumerable<Guid> productIds)
    {
        var idList = productIds.Distinct().ToList();
        return await _context.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && idList.Contains(p.ProductId) && p.IsActive)
            .ToDictionaryAsync(p => p.ProductId);
    }

    public async Task<Dictionary<Guid, WarehouseLocation>> GetActiveLocationsByIdsAsync(Guid tenantId, IEnumerable<Guid> locationIds)
    {
        var idList = locationIds.Distinct().ToList();
        return await _context.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && idList.Contains(l.WarehouseLocationId) && l.IsActive)
            .ToDictionaryAsync(l => l.WarehouseLocationId);
    }

    public async Task<bool> HasTransactionsBySourceAsync(Guid tenantId, SourceDocumentType sourceDocType, Guid sourceDocId)
    {
        return await _context.InventoryTransactions.AsNoTracking().AnyAsync(t =>
            t.TenantId == tenantId &&
            t.SourceDocumentType == sourceDocType &&
            t.SourceDocumentId == sourceDocId);
    }

    public async Task<List<ProductBatchDto>> GetBatchesAsync(Guid tenantId, Guid? productId)
    {
        var query = _context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId);

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            query = query.Where(b => b.ProductId == productId.Value);
        }

        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        return batches.Select(b =>
        {
            products.TryGetValue(b.ProductId, out var prod);

            return new ProductBatchDto
            {
                ProductBatchId = b.ProductBatchId,
                TenantId = b.TenantId,
                ProductId = b.ProductId,
                ProductCode = prod?.ProductCode ?? string.Empty,
                ProductName = prod?.ProductName ?? string.Empty,
                BatchNumber = b.BatchNumber,
                ManufacturingDate = b.ManufacturingDate,
                ExpiryDate = b.ExpiryDate,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt
            };
        }).ToList();
    }

    public async Task<ProductBatchDto?> GetBatchDetailsAsync(Guid tenantId, Guid batchId)
    {
        var batch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductBatchId == batchId);

        if (batch == null) return null;

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == batch.ProductId);

        return new ProductBatchDto
        {
            ProductBatchId = batch.ProductBatchId,
            TenantId = batch.TenantId,
            ProductId = batch.ProductId,
            ProductCode = product?.ProductCode ?? string.Empty,
            ProductName = product?.ProductName ?? string.Empty,
            BatchNumber = batch.BatchNumber,
            ManufacturingDate = batch.ManufacturingDate,
            ExpiryDate = batch.ExpiryDate,
            IsActive = batch.IsActive,
            CreatedAt = batch.CreatedAt
        };
    }

    public async Task<ProductBatch?> GetBatchByIdAsync(Guid tenantId, Guid productBatchId)
    {
        return await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductBatchId == productBatchId);
    }

    public async Task<List<ProductBatch>> GetBatchesListAsync(Guid tenantId, Guid? productId)
    {
        var query = _context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId);

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            query = query.Where(b => b.ProductId == productId.Value);
        }

        return await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task AddProductBatchAsync(ProductBatch batch)
    {
        await _context.ProductBatches.AddAsync(batch);
    }

    public async Task<InventoryPlanningData> GetPlanningDataAsync(Guid tenantId, Guid? warehouseId, Guid? categoryId)
    {
        List<Warehouse> targetWarehouses;
        if (warehouseId.HasValue)
        {
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == warehouseId.Value && w.IsActive);

            if (wh == null)
            {
                throw new KeyNotFoundException($"Warehouse '{warehouseId.Value}' was not found, is inactive, or does not belong to this tenant.");
            }

            targetWarehouses = new List<Warehouse> { wh };
        }
        else
        {
            targetWarehouses = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.TenantId == tenantId && w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .ToListAsync();

            if (targetWarehouses.Count == 0)
            {
                return new InventoryPlanningData();
            }
        }

        var targetWarehouseIds = targetWarehouses.Select(w => w.WarehouseId).ToList();

        var productsQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked);

        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await productsQuery
            .OrderBy(p => p.ProductCode)
            .ToListAsync();

        if (products.Count == 0)
        {
            return new InventoryPlanningData { TargetWarehouses = targetWarehouses };
        }

        var productIds = products.Select(p => p.ProductId).ToList();

        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.CategoryName);

        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                     && targetWarehouseIds.Contains(b.WarehouseId)
                     && productIds.Contains(b.ProductId))
            .ToListAsync();

        var warehouseSettings = await _context.ProductWarehouseSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                     && targetWarehouseIds.Contains(s.WarehouseId)
                     && productIds.Contains(s.ProductId)
                     && s.IsActive)
            .ToListAsync();

        var batchTrackedProductIds = products
            .Where(p => p.TrackingMode == TrackingMode.Batch || p.TrackingMode == TrackingMode.BatchAndSerial)
            .Select(p => p.ProductId)
            .ToList();

        Dictionary<(Guid ProductId, string BatchNumber), DateTime?> batchExpiryDict = new();
        if (batchTrackedProductIds.Count > 0)
        {
            var trackedBatches = await _context.ProductBatches
                .AsNoTracking()
                .Where(pb => pb.TenantId == tenantId
                          && batchTrackedProductIds.Contains(pb.ProductId)
                          && pb.IsActive)
                .Select(pb => new { pb.ProductId, pb.BatchNumber, pb.ExpiryDate })
                .ToListAsync();

            batchExpiryDict = trackedBatches
                .ToDictionary(b => (b.ProductId, b.BatchNumber), b => b.ExpiryDate);
        }

        var approvedPoLines = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId && po.Status == PurchaseOrderStatus.Approved && po.IsActive)
            .SelectMany(po => po.Items.Where(i => i.IsActive && i.Quantity > i.ReceivedQuantity), (po, item) => new ApprovedPoLineItem
            {
                BranchId = po.BranchId,
                ProductId = item.ProductId,
                OutstandingQty = item.Quantity - item.ReceivedQuantity
            })
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync();

        return new InventoryPlanningData
        {
            TargetWarehouses = targetWarehouses,
            Products = products,
            Categories = categories,
            Balances = balances,
            WarehouseSettings = warehouseSettings,
            BatchExpiryDict = batchExpiryDict,
            ApprovedPoLines = approvedPoLines
        };
    }

    public async Task<List<Warehouse>> GetTargetWarehousesAsync(Guid tenantId, Guid? warehouseId)
    {
        if (warehouseId.HasValue)
        {
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == warehouseId.Value && w.IsActive);

            if (wh == null)
            {
                throw new KeyNotFoundException($"Warehouse '{warehouseId.Value}' was not found, is inactive, or does not belong to this tenant.");
            }

            return new List<Warehouse> { wh };
        }

        return await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .ToListAsync();
    }

    public async Task<List<Product>> GetTargetProductsAsync(Guid tenantId, Guid? categoryId)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Dictionary<Guid, decimal>> GetStockOnHandByWarehouseAndProductAsync(
        Guid tenantId,
        IEnumerable<Guid> warehouseIds,
        IEnumerable<Guid> productIds)
    {
        var whList = warehouseIds.ToList();
        var prodList = productIds.ToList();

        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                        && whList.Contains(b.WarehouseId)
                        && prodList.Contains(b.ProductId))
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, TotalOnHand = g.Sum(x => x.QuantityOnHand) })
            .ToListAsync();

        return balances.ToDictionary(x => x.ProductId, x => x.TotalOnHand);
    }

    public async Task<Dictionary<Guid, decimal>> GetApprovedOnOrderQuantitiesAsync(
        Guid tenantId,
        IEnumerable<Guid> productIds)
    {
        var prodList = productIds.ToList();

        var poItems = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId && po.Status == PurchaseOrderStatus.Approved && po.IsActive)
            .SelectMany(po => po.Items.Where(i => i.IsActive && prodList.Contains(i.ProductId)))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, TotalOnOrder = g.Sum(x => x.Quantity - x.ReceivedQuantity) })
            .ToListAsync();

        return poItems.ToDictionary(x => x.ProductId, x => Math.Max(0m, x.TotalOnOrder));
    }

    public async Task<List<GoodsReceiptNote>> GetConfirmedGrnsAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId)
    {
        var grnQuery = _context.GoodsReceiptNotes
            .Include(g => g.Lines)
            .Where(g => g.TenantId == tenantId &&
                        g.CompanyId == companyId &&
                        g.BranchId == branchId &&
                        g.Status == GoodsReceiptNoteStatus.Confirmed);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            grnQuery = grnQuery.Where(g => g.Lines.Any(l => l.WarehouseId == warehouseId.Value));
        }

        return await grnQuery.ToListAsync();
    }

    public async Task<HashSet<Guid>> GetStockTrackedProductIdsAsync(Guid tenantId, IEnumerable<Guid> productIds)
    {
        var idList = productIds.Distinct().ToList();
        var tracked = await _context.Products
            .Where(p => p.TenantId == tenantId && idList.Contains(p.ProductId) && p.IsStockTracked)
            .Select(p => p.ProductId)
            .ToListAsync();

        return tracked.ToHashSet();
    }

    public async Task<List<InventoryTransaction>> GetGrnReceiptTransactionsAsync(Guid tenantId, Guid grnId)
    {
        return await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.SourceDocumentId == grnId &&
                        t.MovementType == InventoryMovementType.GoodsReceipt)
            .ToListAsync();
    }

    public async Task<List<InventoryTransaction>> GetStockAdjustmentInTransactionsAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId)
    {
        var stockReceiptQuery = _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.MovementType == InventoryMovementType.StockAdjustmentIn &&
                        t.SourceDocumentId != Guid.Empty &&
                        t.Quantity > 0);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            stockReceiptQuery = stockReceiptQuery.Where(t => t.WarehouseId == warehouseId.Value);
        }

        return await stockReceiptQuery.ToListAsync();
    }

    public async Task<WarehouseLocation?> GetLocationDetailsAsync(Guid tenantId, Guid locationId)
    {
        return await _context.WarehouseLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == locationId);
    }

    public async Task<LocationType?> GetLocationTypeAsync(Guid tenantId, Guid locationTypeId)
    {
        return await _context.LocationTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.LocationTypeId == locationTypeId);
    }

    public async Task<GoodsReceiptNote?> GetGrnWithLinesAsync(Guid tenantId, Guid grnId)
    {
        return await _context.GoodsReceiptNotes
            .Include(g => g.Lines)
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.GoodsReceiptNoteId == grnId);
    }

    public async Task<List<InventoryTransaction>> GetGrnTransactionsAsync(Guid tenantId, Guid grnId)
    {
        return await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.SourceDocumentId == grnId &&
                        t.SourceDocumentType == SourceDocumentType.GoodsReceiptNote &&
                        t.MovementType == InventoryMovementType.GoodsReceipt)
            .ToListAsync();
    }

    public async Task<List<InventoryTransaction>> GetStockReceiptTransactionsAsync(Guid tenantId, Guid receiptDocId)
    {
        return await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.SourceDocumentId == receiptDocId &&
                        t.MovementType == InventoryMovementType.StockAdjustmentIn)
            .ToListAsync();
    }

    public async Task<Product?> GetProductAsync(Guid tenantId, Guid productId)
    {
        return await _context.Products.FindAsync(productId);
    }

    public async Task<WarehouseLocation?> GetLocationAsync(Guid tenantId, Guid locationId)
    {
        return await _context.WarehouseLocations.FindAsync(locationId);
    }

    public async Task<Warehouse?> GetWarehouseAsync(Guid tenantId, Guid warehouseId)
    {
        return await _context.Warehouses.FindAsync(warehouseId);
    }

    public async Task<UnitOfMeasure?> GetUnitOfMeasureAsync(Guid tenantId, Guid uomId)
    {
        return await _context.UnitsOfMeasure.FindAsync(uomId);
    }

    public Task RemovePutawayLinesAndAllocationsAsync(global::Domain.Features.Inventory.Putaway.Putaway putaway)
    {
        _context.PutawayLineAllocations.RemoveRange(putaway.Lines.SelectMany(l => l.Allocations));
        _context.PutawayLines.RemoveRange(putaway.Lines);
        return Task.CompletedTask;
    }

    public async Task<PutawayReferenceLookups> GetPutawayReferenceLookupsAsync(
        Guid tenantId,
        IEnumerable<Guid> warehouseIds,
        IEnumerable<Guid> productIds,
        IEnumerable<Guid> locationIds,
        IEnumerable<Guid> uomIds)
    {
        var whIds = warehouseIds.Distinct().ToList();
        var prIds = productIds.Distinct().ToList();
        var locIds = locationIds.Distinct().ToList();
        var uIds = uomIds.Distinct().ToList();

        var warehouses = await _context.Warehouses.AsNoTracking()
            .Where(w => whIds.Contains(w.WarehouseId))
            .ToDictionaryAsync(w => w.WarehouseId);

        var products = await _context.Products.AsNoTracking()
            .Where(p => prIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var locations = await _context.WarehouseLocations.AsNoTracking()
            .Where(l => locIds.Contains(l.WarehouseLocationId))
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        var uoms = await _context.UnitsOfMeasure.AsNoTracking()
            .Where(u => uIds.Contains(u.UnitOfMeasureId))
            .ToDictionaryAsync(u => u.UnitOfMeasureId);

        return new PutawayReferenceLookups
        {
            Warehouses = warehouses,
            Products = products,
            Locations = locations,
            UnitsOfMeasure = uoms
        };
    }

    private static string GetPurposeName(StockIssuePurpose purpose) => purpose switch
    {
        StockIssuePurpose.SalesDispatch => "Sales / Customer Dispatch",
        StockIssuePurpose.Manufacturing => "Manufacturing / Material Issue",
        StockIssuePurpose.InternalUse => "Internal Use / Maintenance",
        StockIssuePurpose.Other => "Other Authorized Purpose",
        _ => purpose.ToString()
    };

    private static string GetReceiptReasonName(StockReceiptReason reason) => reason switch
    {
        StockReceiptReason.OpeningBalance => "Opening / Initial Stock Balance",
        StockReceiptReason.CustomerReturn => "Customer Sales Return",
        StockReceiptReason.InternalProduction => "Internal Production / Assembly",
        StockReceiptReason.Miscellaneous => "Miscellaneous Authorized Inflow",
        _ => reason.ToString()
    };

    private static StockIssuePurpose MapMovementTypeToPurpose(InventoryMovementType type, SourceDocumentType? sourceDocType = null) => type switch
    {
        InventoryMovementType.ShipmentDispatch => StockIssuePurpose.SalesDispatch,
        InventoryMovementType.ProductionIssue => StockIssuePurpose.Manufacturing,
        InventoryMovementType.StockAdjustmentOut when sourceDocType == SourceDocumentType.ManualPosting => StockIssuePurpose.Other,
        InventoryMovementType.StockAdjustmentOut => StockIssuePurpose.InternalUse,
        _ => StockIssuePurpose.Other
    };

    private static StockReceiptReason MapMovementTypeToReceiptReason(InventoryMovementType type) => type switch
    {
        InventoryMovementType.ProductionReceipt => StockReceiptReason.InternalProduction,
        _ => StockReceiptReason.OpeningBalance
    };
}
