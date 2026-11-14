using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory;

public class InventoryService : IInventoryService
{
    private readonly DomainDbContext _context;
    private readonly IInventoryPostingService _postingService;
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(
        DomainDbContext context,
        IInventoryPostingService postingService,
        IInventoryRepository? inventoryRepository = null)
    {
        _context = context;
        _postingService = postingService;
        _inventoryRepository = inventoryRepository ?? new InventoryRepository(context);
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(Guid tenantId)
    {
        // Active stock-tracked items
        var totalStockItems = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked)
            .CountAsync();

        // Active warehouses
        var totalWarehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .CountAsync();

        // Active locations
        var totalLocations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(wl => wl.TenantId == tenantId && wl.IsActive)
            .CountAsync();

        // Total inventory ledger transactions
        var totalTransactions = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .CountAsync();

        // Total physical on-hand quantity across all balances
        var totalOnHand = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .SumAsync(b => (decimal?)b.QuantityOnHand) ?? 0m;

        // Total inventory valuation across valuation balance records
        var totalValuation = await _context.ProductValuationBalances
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .SumAsync(v => (decimal?)v.TotalCostValue) ?? 0m;

        // Calculate low-stock item count (reorder level > 0 and current on-hand <= reorder level)
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

        return new InventorySummaryDto
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

    public async Task<List<InventoryRecentTransactionDto>> GetRecentTransactionsAsync(Guid tenantId, int limit = 10)
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

    public async Task<List<StockIssueDto>> GetStockIssuesAsync(Guid tenantId, Guid? warehouseId = null, StockIssuePurpose? purpose = null)
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

    public async Task<AvailableStockDto> GetAvailableStockAsync(Guid tenantId, Guid productId, Guid locationId, string? batchNumber = null, string? serialNumber = null)
    {
        var balance = await _postingService.GetBalanceAsync(tenantId, locationId, productId, batchNumber, serialNumber);
        return new AvailableStockDto
        {
            ProductId = productId,
            WarehouseLocationId = locationId,
            QuantityOnHand = balance?.QuantityOnHand ?? 0m,
            QuantityAllocated = balance?.QuantityAllocated ?? 0m,
            QuantityQuarantine = balance?.QuantityQuarantine ?? 0m,
            QuantityAvailable = balance?.QuantityAvailable ?? 0m
        };
    }

    public async Task<List<StockIssueDto>> CreateStockIssueAsync(Guid tenantId, CreateStockIssueRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one stock issue line is required.");
        if (request.WarehouseId == Guid.Empty)
            throw new ArgumentException("Warehouse is required.");

        var warehouse = await _context.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == request.WarehouseId && w.IsActive)
            ?? throw new KeyNotFoundException("Warehouse not found or is inactive.");

        var movementType = request.Purpose switch
        {
            StockIssuePurpose.SalesDispatch => InventoryMovementType.ShipmentDispatch,
            StockIssuePurpose.Manufacturing => InventoryMovementType.ProductionIssue,
            StockIssuePurpose.InternalUse => InventoryMovementType.StockAdjustmentOut,
            _ => InventoryMovementType.StockAdjustmentOut
        };

        var sourceDocType = request.Purpose switch
        {
            StockIssuePurpose.SalesDispatch => SourceDocumentType.SalesShipment,
            StockIssuePurpose.Manufacturing => SourceDocumentType.ProductionOrder,
            StockIssuePurpose.InternalUse => SourceDocumentType.StockAdjustment,
            _ => SourceDocumentType.ManualPosting
        };

        Guid issueBatchId;
        if (request.StockIssueId.HasValue && request.StockIssueId.Value != Guid.Empty)
        {
            issueBatchId = request.StockIssueId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceDocumentReference))
        {
            if (Guid.TryParse(request.SourceDocumentReference.Trim(), out var parsedRef))
            {
                issueBatchId = parsedRef;
            }
            else
            {
                issueBatchId = DeriveDeterministicGuid(tenantId, sourceDocType.ToString(), request.SourceDocumentReference.Trim());
            }
        }
        else
        {
            issueBatchId = Guid.NewGuid();
        }

        // Prevention of duplicate posting / repeated API requests
        if (request.StockIssueId.HasValue || !string.IsNullOrWhiteSpace(request.SourceDocumentReference))
        {
            bool alreadyPosted = await _context.InventoryTransactions.AsNoTracking().AnyAsync(t =>
                t.TenantId == tenantId &&
                t.SourceDocumentType == sourceDocType &&
                t.SourceDocumentId == issueBatchId &&
                t.MovementType == movementType);

            if (alreadyPosted)
            {
                throw new InvalidOperationException($"Stock issue '{issueBatchId}' has already been posted.");
            }
        }

        // Validate line inputs and collect entity metadata
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = request.Lines.Select(l => l.WarehouseLocationId).Distinct().ToList();

        var products = await _context.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId) && p.IsActive)
            .ToDictionaryAsync(p => p.ProductId);

        var locations = await _context.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && locationIds.Contains(l.WarehouseLocationId) && l.IsActive)
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Issue quantity must be greater than zero for all lines.");
            if (line.ProductId == Guid.Empty)
                throw new ArgumentException("Product is required for each line.");
            if (line.WarehouseLocationId == Guid.Empty)
                throw new ArgumentException("Warehouse storage location is required for each line.");

            if (!products.TryGetValue(line.ProductId, out var product))
                throw new KeyNotFoundException($"Product '{line.ProductId}' not found or is inactive.");

            if (!product.IsStockTracked)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not a stock-tracked item.");

            if (!locations.TryGetValue(line.WarehouseLocationId, out var location))
                throw new KeyNotFoundException($"Storage location '{line.WarehouseLocationId}' not found or is inactive.");

            if (location.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException($"Location '{location.LocationCode}' does not belong to warehouse '{warehouse.WarehouseCode}'.");

            if (product.TrackingMode == TrackingMode.None)
            {
                if (!string.IsNullOrWhiteSpace(line.BatchNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track batches. BatchNumber must be empty.");
                if (!string.IsNullOrWhiteSpace(line.SerialNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track serial numbers. SerialNumber must be empty.");
            }

            // Batch tracking validation
            if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.BatchNumber))
                {
                    throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                }
            }

            // Serial tracking validation
            if (product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.SerialNumber))
                {
                    throw new InvalidOperationException($"Serial number is required for serial-tracked product '{product.ProductCode}'.");
                }
                if (line.Quantity != 1)
                {
                    throw new InvalidOperationException($"Serial-tracked items must have quantity of 1. Specified: {line.Quantity} for product '{product.ProductCode}'.");
                }
            }
        }

        // Verify unique serial numbers across lines in this request
        var serializedLines = request.Lines
            .Where(l => products.TryGetValue(l.ProductId, out var p) && (p.TrackingMode == TrackingMode.Serial || p.TrackingMode == TrackingMode.BatchAndSerial))
            .ToList();

        var duplicateSerial = serializedLines
            .GroupBy(l => (l.ProductId, SerialNumber: l.SerialNumber?.Trim().ToUpperInvariant() ?? string.Empty))
            .FirstOrDefault(g => !string.IsNullOrEmpty(g.Key.SerialNumber) && g.Count() > 1);

        if (duplicateSerial != null)
        {
            var p = products[duplicateSerial.Key.ProductId];
            throw new InvalidOperationException(
                $"Duplicate serial number '{duplicateSerial.Key.SerialNumber}' specified across multiple lines for product '{p.ProductCode}'. Each serial number can only be issued once.");
        }

        var issueDate = (request.IssueDate ?? DateTime.UtcNow).Date;

        // Validate stock availability aggregating requested quantities across all lines
        var groupedDemands = request.Lines
            .GroupBy(l => (
                l.ProductId,
                l.WarehouseLocationId,
                BatchNumber: l.BatchNumber?.Trim() ?? string.Empty,
                SerialNumber: l.SerialNumber?.Trim() ?? string.Empty))
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseLocationId,
                g.Key.BatchNumber,
                g.Key.SerialNumber,
                TotalQuantity = g.Sum(x => x.Quantity)
            });

        foreach (var demand in groupedDemands)
        {
            if (!string.IsNullOrEmpty(demand.BatchNumber))
            {
                var batchInTenant = await _inventoryRepository.GetBatchByNumberAsync(tenantId, demand.BatchNumber);
                if (batchInTenant != null && batchInTenant.ProductId != demand.ProductId)
                {
                    throw new InvalidOperationException(
                        $"Batch '{demand.BatchNumber}' is already associated with another product in this tenant. A batch number cannot be reused for a different product.");
                }

                var batch = await _inventoryRepository.GetBatchAsync(tenantId, demand.ProductId, demand.BatchNumber);
                if (batch != null)
                {
                    if (!batch.IsActive)
                    {
                        var prod = products[demand.ProductId];
                        throw new InvalidOperationException(
                            $"Batch '{demand.BatchNumber}' for product '{prod.ProductCode}' is inactive and cannot be issued or dispatched.");
                    }

                    if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < issueDate)
                    {
                        var prod = products[demand.ProductId];
                        throw new InvalidOperationException(
                            $"Batch '{demand.BatchNumber}' for product '{prod.ProductCode}' expired on {batch.ExpiryDate.Value:yyyy-MM-dd} and cannot be issued.");
                    }
                }
            }

            var balance = await _postingService.GetBalanceAsync(
                tenantId,
                demand.WarehouseLocationId,
                demand.ProductId,
                demand.BatchNumber,
                demand.SerialNumber);

            decimal available = balance?.QuantityAvailable ?? 0m;
            if (demand.TotalQuantity > available)
            {
                var prod = products[demand.ProductId];
                var loc = locations[demand.WarehouseLocationId];
                throw new InvalidOperationException(
                    $"Insufficient available stock for '{prod.ProductCode} - {prod.ProductName}' at location '{loc.LocationCode}'. Available: {available}, Requested: {demand.TotalQuantity}");
            }
        }

        // Execute all line postings inside a single ambient database transaction
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var existingTx = _context.Database.CurrentTransaction;
            var dbTransaction = existingTx == null ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var results = new List<StockIssueDto>();
                int sequence = 1;

                foreach (var line in request.Lines)
                {
                    Guid lineId = line.StockIssueLineId ?? DeriveDeterministicGuid(tenantId, issueBatchId.ToString(), $"line_{sequence}");

                    var postRequest = new PostInventoryMovementRequest
                    {
                        MovementType = movementType,
                        SourceDocumentType = sourceDocType,
                        SourceDocumentId = issueBatchId,
                        SourceDocumentLineId = lineId,
                        MovementSequence = sequence++,
                        ProductId = line.ProductId,
                        WarehouseId = request.WarehouseId,
                        WarehouseLocationId = line.WarehouseLocationId,
                        Quantity = line.Quantity,
                        BatchNumber = line.BatchNumber,
                        SerialNumber = line.SerialNumber,
                        TransactionDate = request.IssueDate ?? DateTime.UtcNow
                    };

                    var tx = await _postingService.PostMovementAsync(tenantId, postRequest);
                    var product = products[line.ProductId];
                    var location = locations[line.WarehouseLocationId];

                    results.Add(new StockIssueDto
                    {
                        StockIssueId = tx.InventoryTransactionId,
            StockIssueLineId = tx.SourceDocumentLineId != Guid.Empty ? tx.SourceDocumentLineId : null,
                        IssueNumber = tx.TransactionNumber,
                        IssueDate = tx.TransactionDate,
                        Purpose = request.Purpose,
                        PurposeName = GetPurposeName(request.Purpose),
                        MovementType = tx.MovementType.ToString(),
                        Status = StockIssueStatus.Posted,
                        StatusName = "Posted",
                        WarehouseId = warehouse.WarehouseId,
                        WarehouseCode = warehouse.WarehouseCode,
                        WarehouseName = warehouse.WarehouseName,
                        WarehouseLocationId = location.WarehouseLocationId,
                        LocationCode = location.LocationCode,
                        LocationName = location.LocationName,
                        ProductId = product.ProductId,
                        ProductCode = product.ProductCode,
                        ProductName = product.ProductName,
                        UnitOfMeasure = product.UnitOfMeasure,
                        Quantity = Math.Abs(tx.Quantity),
                        UnitCost = tx.UnitCost,
                        TotalCost = tx.TotalCost,
                        BatchNumber = tx.BatchNumber,
                        SerialNumber = tx.SerialNumber,
                        SourceDocumentReference = request.SourceDocumentReference,
                        Remarks = line.Remarks ?? request.Remarks,

            CreatedAt = tx.CreatedAt
                    });
                }

                if (dbTransaction != null)
                {
                    await dbTransaction.CommitAsync();
                }

                return results;
            }
            catch
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.DisposeAsync();
                }
            }
        });
    }

    public async Task<List<StockReceiptDto>> GetStockReceiptsAsync(Guid tenantId, Guid? warehouseId = null, StockReceiptReason? reason = null)
    {
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Quantity > 0 && t.MovementType != InventoryMovementType.GoodsReceipt);

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

        var product = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == t.ProductId);

        var warehouse = await _context.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == t.WarehouseId);

        var location = await _context.WarehouseLocations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == t.WarehouseLocationId);

        DateTime? mfgDate = null;
        DateTime? expDate = t.ExpiryDate;
        if (!string.IsNullOrEmpty(t.BatchNumber))
        {
            var batch = await _context.ProductBatches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == t.ProductId && b.BatchNumber == t.BatchNumber);
            if (batch != null)
            {
                mfgDate = batch.ManufacturingDate;
                expDate ??= batch.ExpiryDate;
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

    public async Task<List<StockReceiptDto>> CreateStockReceiptAsync(Guid tenantId, CreateStockReceiptRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.WarehouseId == Guid.Empty)
            throw new ArgumentException("Warehouse is required.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line item is required to receive stock.");

        var warehouse = await _context.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == request.WarehouseId && w.IsActive)
            ?? throw new KeyNotFoundException($"Warehouse '{request.WarehouseId}' not found or is inactive.");

        var movementType = request.Reason switch
        {
            StockReceiptReason.InternalProduction => InventoryMovementType.ProductionReceipt,
            _ => InventoryMovementType.StockAdjustmentIn
        };

        var sourceDocType = request.Reason switch
        {
            StockReceiptReason.InternalProduction => SourceDocumentType.ProductionOrder,
            _ => SourceDocumentType.StockAdjustment
        };

        Guid receiptBatchId;
        if (request.StockReceiptId.HasValue && request.StockReceiptId.Value != Guid.Empty)
        {
            receiptBatchId = request.StockReceiptId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.SourceDocumentReference))
        {
            if (Guid.TryParse(request.SourceDocumentReference.Trim(), out var parsedRef))
            {
                receiptBatchId = parsedRef;
            }
            else
            {
                receiptBatchId = DeriveDeterministicGuid(tenantId, sourceDocType.ToString(), request.SourceDocumentReference.Trim());
            }
        }
        else
        {
            receiptBatchId = Guid.NewGuid();
        }

        // Prevention of duplicate posting / repeated API requests
        if (request.StockReceiptId.HasValue || !string.IsNullOrWhiteSpace(request.SourceDocumentReference))
        {
            bool alreadyPosted = await _context.InventoryTransactions.AsNoTracking().AnyAsync(t =>
                t.TenantId == tenantId &&
                t.SourceDocumentType == sourceDocType &&
                t.SourceDocumentId == receiptBatchId &&
                t.MovementType == movementType);

            if (alreadyPosted)
            {
                throw new InvalidOperationException($"Stock receipt '{receiptBatchId}' has already been posted.");
            }
        }

        // Validate line inputs and collect entity metadata
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = request.Lines.Select(l => l.WarehouseLocationId).Distinct().ToList();

        var products = await _context.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId) && p.IsActive)
            .ToDictionaryAsync(p => p.ProductId);

        var locations = await _context.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && locationIds.Contains(l.WarehouseLocationId) && l.IsActive)
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Receipt quantity must be greater than zero for all lines.");
            if (line.ProductId == Guid.Empty)
                throw new ArgumentException("Product is required for each line.");
            if (line.WarehouseLocationId == Guid.Empty)
                throw new ArgumentException("Warehouse storage location is required for each line.");

            if (!products.TryGetValue(line.ProductId, out var product))
                throw new KeyNotFoundException($"Product '{line.ProductId}' not found or is inactive.");

            if (!product.IsStockTracked)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not a stock-tracked item.");

            if (!locations.TryGetValue(line.WarehouseLocationId, out var location))
                throw new KeyNotFoundException($"Storage location '{line.WarehouseLocationId}' not found or is inactive.");

            if (location.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException($"Location '{location.LocationCode}' does not belong to warehouse '{warehouse.WarehouseCode}'.");

            // Batch tracking validation
            if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.BatchNumber))
                {
                    throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                }
            }

            // Serial tracking validation
            if (product.TrackingMode == TrackingMode.Serial || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.SerialNumber))
                {
                    throw new InvalidOperationException($"Serial number is required for serial-tracked product '{product.ProductCode}'.");
                }
                if (line.Quantity != 1)
                {
                    throw new InvalidOperationException($"Serial-tracked items must have quantity of 1. Specified: {line.Quantity} for product '{product.ProductCode}'.");
                }
            }

            // Date validation
            if (line.ManufacturingDate.HasValue && line.ExpiryDate.HasValue && line.ExpiryDate.Value.Date < line.ManufacturingDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Expiry date ({line.ExpiryDate.Value:yyyy-MM-dd}) cannot be earlier than manufacturing date ({line.ManufacturingDate.Value:yyyy-MM-dd}).");
            }
        }

        // Execute all line postings inside a single ambient database transaction
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            var existingTx = _context.Database.CurrentTransaction;
            var dbTransaction = existingTx == null ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var results = new List<StockReceiptDto>();
                int sequence = 1;

                foreach (var line in request.Lines)
                {
                    Guid lineId = line.StockReceiptLineId ?? DeriveDeterministicGuid(tenantId, receiptBatchId.ToString(), $"line_{sequence}");
                    var product = products[line.ProductId];
                    var location = locations[line.WarehouseLocationId];
                    decimal unitCost = line.UnitCost ?? product.StandardCost;

                    var postRequest = new PostInventoryMovementRequest
                    {
                        MovementType = movementType,
                        SourceDocumentType = sourceDocType,
                        SourceDocumentId = receiptBatchId,
                        SourceDocumentLineId = lineId,
                        MovementSequence = sequence++,
                        ProductId = line.ProductId,
                        WarehouseId = request.WarehouseId,
                        WarehouseLocationId = line.WarehouseLocationId,
                        Quantity = line.Quantity,
                        UnitCost = unitCost,
                        BatchNumber = line.BatchNumber?.Trim(),
                        SerialNumber = line.SerialNumber?.Trim(),
                        ManufacturingDate = line.ManufacturingDate,
                        ExpiryDate = line.ExpiryDate,
                        TransactionDate = request.ReceiptDate ?? DateTime.UtcNow
                    };

                    var tx = await _postingService.PostMovementAsync(tenantId, postRequest);

                    results.Add(new StockReceiptDto
                    {
                        StockReceiptId = tx.InventoryTransactionId,
                        StockReceiptLineId = lineId,
                        ReceiptNumber = tx.TransactionNumber,
                        ReceiptDate = tx.TransactionDate,
                        Reason = request.Reason,
                        ReasonName = GetReceiptReasonName(request.Reason),
                        MovementType = tx.MovementType.ToString(),
                        Status = StockReceiptStatus.Posted,
                        StatusName = "Posted",
                        WarehouseId = warehouse.WarehouseId,
                        WarehouseCode = warehouse.WarehouseCode,
                        WarehouseName = warehouse.WarehouseName,
                        WarehouseLocationId = location.WarehouseLocationId,
                        LocationCode = location.LocationCode,
                        LocationName = location.LocationName,
                        ProductId = product.ProductId,
                        ProductCode = product.ProductCode,
                        ProductName = product.ProductName,
                        UnitOfMeasure = product.UnitOfMeasure,
                        Quantity = tx.Quantity,
                        UnitCost = tx.UnitCost,
                        TotalCost = tx.TotalCost,
                        BatchNumber = tx.BatchNumber,
                        SerialNumber = tx.SerialNumber,
                        ManufacturingDate = postRequest.ManufacturingDate,
                        ExpiryDate = tx.ExpiryDate ?? postRequest.ExpiryDate,
                        SourceDocumentReference = request.SourceDocumentReference,
                        Remarks = line.Remarks ?? request.Remarks,

            CreatedAt = tx.CreatedAt
                    });
                }

                if (dbTransaction != null)
                {
                    await dbTransaction.CommitAsync();
                }

                return results;
            }
            catch (Exception)
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.DisposeAsync();
                }
            }
        });
    }

    public static string GetPurposeName(StockIssuePurpose purpose) => purpose switch
    {
        StockIssuePurpose.SalesDispatch => "Sales / Customer Dispatch",
        StockIssuePurpose.Manufacturing => "Manufacturing / Material Issue",
        StockIssuePurpose.InternalUse => "Internal Use / Maintenance",
        StockIssuePurpose.Other => "Other Authorized Purpose",
        _ => purpose.ToString()
    };

    public static string GetReceiptReasonName(StockReceiptReason reason) => reason switch
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

    private static Guid DeriveDeterministicGuid(Guid tenantId, string scope, string identifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes($"{tenantId:D}:{scope}:{identifier}");
        var hash = sha256.ComputeHash(inputBytes);
        return new Guid(hash.AsSpan(0, 16));
    }

    private static StockReceiptReason MapMovementTypeToReceiptReason(InventoryMovementType type) => type switch
    {
        InventoryMovementType.ProductionReceipt => StockReceiptReason.InternalProduction,
        InventoryMovementType.StockAdjustmentIn => StockReceiptReason.Miscellaneous,
        _ => StockReceiptReason.OpeningBalance
    };

    public async Task<StockByLocationReportDto> GetStockByLocationReportAsync(Guid tenantId, StockByLocationReportQuery query)
    {
        query ??= new StockByLocationReportQuery();

        // 1. Base query for balances with positive on-hand quantity within tenant
        var balanceQuery = _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.QuantityOnHand > 0);

        if (query.WarehouseId.HasValue && query.WarehouseId.Value != Guid.Empty)
        {
            balanceQuery = balanceQuery.Where(b => b.WarehouseId == query.WarehouseId.Value);
        }

        if (query.LocationId.HasValue && query.LocationId.Value != Guid.Empty)
        {
            balanceQuery = balanceQuery.Where(b => b.WarehouseLocationId == query.LocationId.Value);
        }

        if (query.ProductId.HasValue && query.ProductId.Value != Guid.Empty)
        {
            balanceQuery = balanceQuery.Where(b => b.ProductId == query.ProductId.Value);
        }

        var balances = await balanceQuery.ToListAsync();
        if (balances.Count == 0)
        {
            return new StockByLocationReportDto
            {
                Page = query.Page ?? 1,
                PageSize = query.PageSize ?? 25,
                TotalCount = 0,
                TotalQuantityOnHand = 0,
                TotalQuantityAvailable = 0,
                TotalStockValue = 0,
                Currency = "LKR",
                Items = new List<StockByLocationRowDto>()
            };
        }

        // 2. Fetch master data referenced by these balances
        var productIds = balances.Select(b => b.ProductId).Distinct().ToList();
        var warehouseIds = balances.Select(b => b.WarehouseId).Distinct().ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .ToDictionaryAsync(c => c.CategoryId);

        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && warehouseIds.Contains(w.WarehouseId))
            .ToDictionaryAsync(w => w.WarehouseId);

        var allTenantLocations = await _context.WarehouseLocations
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && warehouseIds.Contains(l.WarehouseId))
            .ToListAsync();
        var locationDict = allTenantLocations.ToDictionary(l => l.WarehouseLocationId);

        var zones = await _context.WarehouseZones
            .AsNoTracking()
            .Where(z => z.TenantId == tenantId && warehouseIds.Contains(z.WarehouseId))
            .ToDictionaryAsync(z => z.WarehouseZoneId);

        var locationTypes = await _context.LocationTypes
            .AsNoTracking()
            .Where(lt => lt.TenantId == tenantId || lt.TenantId == null)
            .ToDictionaryAsync(lt => lt.LocationTypeId);

        // Fetch companies for currency
        var companyIds = warehouses.Values.Where(w => w.CompanyId.HasValue).Select(w => w.CompanyId!.Value).Distinct().ToList();
        var companies = await _context.Companies
            .AsNoTracking()
            .Include(c => c.BaseCurrency)
            .Where(c => c.TenantId == tenantId && companyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // Fetch valuation balances & cost layers for these products & warehouses
        var valBalances = await _context.ProductValuationBalances
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && productIds.Contains(v.ProductId) && warehouseIds.Contains(v.WarehouseId))
            .ToListAsync();

        var costLayers = await _context.InventoryCostLayers
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && productIds.Contains(l.ProductId) && warehouseIds.Contains(l.WarehouseId) && !l.IsExhausted && l.RemainingQuantity > 0)
            .ToListAsync();

        // 3. Helper to build HierarchyPath
        string BuildHierarchyPath(ERP.Domain.Features.MasterData.Warehouse.WarehouseLocation loc)
        {
            var parts = new List<string>();
            if (loc.WarehouseZoneId.HasValue && zones.TryGetValue(loc.WarehouseZoneId.Value, out var z))
            {
                parts.Add(z.ZoneName);
            }
            var ancestors = new List<string>();
            var curr = loc.ParentLocationId;
            var visited = new HashSet<Guid> { loc.WarehouseLocationId };
            while (curr.HasValue && locationDict.TryGetValue(curr.Value, out var parentNode))
            {
                if (!visited.Add(curr.Value)) break;
                ancestors.Insert(0, parentNode.LocationCode);
                curr = parentNode.ParentLocationId;
            }
            parts.AddRange(ancestors);
            parts.Add(loc.LocationCode);
            return string.Join(" / ", parts);
        }

        // Helper to determine classification
        string DetermineClassification(
            ERP.Domain.Features.MasterData.Warehouse.WarehouseLocation loc,
            ERP.Domain.Features.MasterData.Warehouse.LocationType? lt,
            ERP.Domain.Features.MasterData.Warehouse.WarehouseZone? z)
        {
            var typeCode = lt?.Code?.ToUpperInvariant() ?? string.Empty;
            var zoneCode = z?.ZoneCode?.ToUpperInvariant() ?? string.Empty;
            var locName = loc.LocationName ?? string.Empty;

            bool isStaging = typeCode == "STAGING" || typeCode == "RECEIVING" ||
                             zoneCode == "ZONE-REC" ||
                             locName.IndexOf("Receiving", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             locName.IndexOf("Staging", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isStaging)
            {
                return "Staging";
            }

            bool canStore = lt?.CanStoreInventory ?? true;
            bool canPutAway = lt?.CanPutAway ?? true;
            if (canStore && canPutAway && loc.IsActive)
            {
                return "Storage";
            }

            return "Other";
        }

        // 4. Map rows and filter
        var rows = new List<StockByLocationRowDto>(balances.Count);
        string defaultCurrency = "LKR";

        foreach (var b in balances)
        {
            if (!products.TryGetValue(b.ProductId, out var product)) continue;
            if (!warehouses.TryGetValue(b.WarehouseId, out var warehouse)) continue;
            if (!locationDict.TryGetValue(b.WarehouseLocationId, out var location)) continue;

            // Company & Branch filters
            if (query.CompanyId.HasValue && query.CompanyId.Value != Guid.Empty && warehouse.CompanyId != query.CompanyId.Value)
            {
                continue;
            }
            if (query.BranchId.HasValue && query.BranchId.Value != Guid.Empty && warehouse.BranchId != query.BranchId.Value)
            {
                continue;
            }

            // Category filter
            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty && product.CategoryId != query.CategoryId.Value)
            {
                continue;
            }

            zones.TryGetValue(location.WarehouseZoneId ?? Guid.Empty, out var zone);
            ERP.Domain.Features.MasterData.Warehouse.LocationType? locType = null;
            if (location.LocationTypeId.HasValue)
            {
                locationTypes.TryGetValue(location.LocationTypeId.Value, out locType);
            }

            var classification = DetermineClassification(location, locType, zone);

            // Classification filter: "storage" or "staging"
            if (!string.IsNullOrWhiteSpace(query.LocationClassification))
            {
                var targetClass = query.LocationClassification.Trim().ToLowerInvariant();
                if (targetClass == "storage" && classification != "Storage") continue;
                if (targetClass == "staging" && classification != "Staging") continue;
            }

            categories.TryGetValue(product.CategoryId, out var category);

            string currency = defaultCurrency;
            if (warehouse.CompanyId.HasValue && companies.TryGetValue(warehouse.CompanyId.Value, out var comp) && !string.IsNullOrWhiteSpace(comp.BaseCurrencyCode))
            {
                currency = comp.BaseCurrencyCode;
                defaultCurrency = currency;
            }

            // Valuation
            decimal unitCost = 0;
            decimal totalCostValue = 0;

            if (product.ValuationMethod == ValuationMethod.StandardCost)
            {
                unitCost = product.StandardCost;
                totalCostValue = Math.Round(b.QuantityOnHand * unitCost, 2, MidpointRounding.AwayFromZero);
            }
            else if (product.ValuationMethod == ValuationMethod.WeightedAverage)
            {
                var vb = valBalances.FirstOrDefault(v => v.ProductId == b.ProductId && v.WarehouseId == b.WarehouseId);
                unitCost = (vb != null && vb.CurrentAverageCost > 0) ? vb.CurrentAverageCost : product.StandardCost;
                totalCostValue = Math.Round(b.QuantityOnHand * unitCost, 2, MidpointRounding.AwayFromZero);
            }
            else // FIFO or LIFO
            {
                // Check location-specific cost layers
                var locLayers = costLayers
                    .Where(l => l.ProductId == b.ProductId && l.WarehouseId == b.WarehouseId && l.WarehouseLocationId == b.WarehouseLocationId)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(b.BatchNumber))
                {
                    var batchLayers = locLayers.Where(l => l.BatchNumber == b.BatchNumber).ToList();
                    if (batchLayers.Count > 0) locLayers = batchLayers;
                }

                decimal locRemainingQty = locLayers.Sum(l => l.RemainingQuantity);
                decimal locRemainingCost = locLayers.Sum(l => l.RemainingCost);

                if (locRemainingQty > 0)
                {
                    unitCost = Math.Round(locRemainingCost / locRemainingQty, 4, MidpointRounding.AwayFromZero);
                    totalCostValue = Math.Round(b.QuantityOnHand * unitCost, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    // Fallback to warehouse-level active FIFO layers for this product
                    var whLayers = costLayers.Where(l => l.ProductId == b.ProductId && l.WarehouseId == b.WarehouseId).ToList();
                    decimal whQty = whLayers.Sum(l => l.RemainingQuantity);
                    decimal whCost = whLayers.Sum(l => l.RemainingCost);
                    unitCost = whQty > 0 ? Math.Round(whCost / whQty, 4, MidpointRounding.AwayFromZero) : product.StandardCost;
                    totalCostValue = Math.Round(b.QuantityOnHand * unitCost, 2, MidpointRounding.AwayFromZero);
                }
            }

            var row = new StockByLocationRowDto
            {
                InventoryBalanceId = b.InventoryBalanceId,
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Sku = product.ProductCode,
                CategoryId = product.CategoryId != Guid.Empty ? product.CategoryId : null,
                CategoryName = category?.CategoryName ?? "General",
                UnitOfMeasure = product.UnitOfMeasure,

                WarehouseId = warehouse.WarehouseId,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseName = warehouse.WarehouseName,

                WarehouseLocationId = location.WarehouseLocationId,
                LocationCode = location.LocationCode,
                LocationName = location.LocationName,
                ZoneCode = zone?.ZoneCode,
                ZoneName = zone?.ZoneName,
                LocationTypeCode = locType?.Code,
                LocationTypeName = locType?.Name,
                HierarchyPath = BuildHierarchyPath(location),
                LocationClassification = classification,

                BatchNumber = b.BatchNumber,
                SerialNumber = b.SerialNumber,

                QuantityOnHand = b.QuantityOnHand,
                QuantityAllocated = b.QuantityAllocated,
                QuantityQuarantine = b.QuantityQuarantine,
                QuantityAvailable = b.QuantityAvailable,

                UnitCost = unitCost,
                TotalCostValue = totalCostValue,
                ValuationMethod = product.ValuationMethod.ToString(),
                Currency = currency
            };

            // Global search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim().ToLowerInvariant();
                bool matches = row.ProductCode.ToLowerInvariant().Contains(term) ||
                               row.ProductName.ToLowerInvariant().Contains(term) ||
                               row.Sku.ToLowerInvariant().Contains(term) ||
                               row.LocationCode.ToLowerInvariant().Contains(term) ||
                               row.LocationName.ToLowerInvariant().Contains(term) ||
                               row.HierarchyPath.ToLowerInvariant().Contains(term) ||
                               row.BatchNumber.ToLowerInvariant().Contains(term) ||
                               row.SerialNumber.ToLowerInvariant().Contains(term) ||
                               (row.CategoryName != null && row.CategoryName.ToLowerInvariant().Contains(term));
                if (!matches) continue;
            }

            rows.Add(row);
        }

        // 5. Total aggregations across ALL matching rows
        int totalCount = rows.Count;
        decimal totalOnHand = rows.Sum(r => r.QuantityOnHand);
        decimal totalAvailable = rows.Sum(r => r.QuantityAvailable);
        decimal totalValue = rows.Sum(r => r.TotalCostValue);

        // 6. Sorting
        var sortCol = query.SortBy?.Trim().ToLowerInvariant();
        bool desc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<StockByLocationRowDto> ordered = sortCol switch
        {
            "productcode" => desc ? rows.OrderByDescending(r => r.ProductCode) : rows.OrderBy(r => r.ProductCode),
            "productname" => desc ? rows.OrderByDescending(r => r.ProductName) : rows.OrderBy(r => r.ProductName),
            "sku" => desc ? rows.OrderByDescending(r => r.Sku) : rows.OrderBy(r => r.Sku),
            "categoryname" => desc ? rows.OrderByDescending(r => r.CategoryName) : rows.OrderBy(r => r.CategoryName),
            "warehousecode" => desc ? rows.OrderByDescending(r => r.WarehouseCode) : rows.OrderBy(r => r.WarehouseCode),
            "locationcode" => desc ? rows.OrderByDescending(r => r.LocationCode) : rows.OrderBy(r => r.LocationCode),
            "locationname" => desc ? rows.OrderByDescending(r => r.LocationName) : rows.OrderBy(r => r.LocationName),
            "quantityonhand" => desc ? rows.OrderByDescending(r => r.QuantityOnHand) : rows.OrderBy(r => r.QuantityOnHand),
            "quantityavailable" => desc ? rows.OrderByDescending(r => r.QuantityAvailable) : rows.OrderBy(r => r.QuantityAvailable),
            "unitcost" => desc ? rows.OrderByDescending(r => r.UnitCost) : rows.OrderBy(r => r.UnitCost),
            "totalcostvalue" => desc ? rows.OrderByDescending(r => r.TotalCostValue) : rows.OrderBy(r => r.TotalCostValue),
            "locationclassification" => desc ? rows.OrderByDescending(r => r.LocationClassification) : rows.OrderBy(r => r.LocationClassification),
            _ => rows.OrderBy(r => r.LocationCode).ThenBy(r => r.ProductCode)
        };

        // 7. Pagination
        int page = query.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;
        int pageSize = query.PageSize.GetValueOrDefault(25);

        List<StockByLocationRowDto> pagedItems;
        if (pageSize > 0)
        {
            pagedItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            pagedItems = ordered.ToList();
            pageSize = totalCount;
        }

        return new StockByLocationReportDto
        {
            Items = pagedItems,
            TotalCount = totalCount,
            TotalQuantityOnHand = totalOnHand,
            TotalQuantityAvailable = totalAvailable,
            TotalStockValue = Math.Round(totalValue, 2, MidpointRounding.AwayFromZero),
            Currency = defaultCurrency,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<StockLedgerReportDto> GetStockLedgerReportAsync(Guid tenantId, StockLedgerReportQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        if (query.ProductId == Guid.Empty || query.WarehouseId == Guid.Empty)
        {
            return new StockLedgerReportDto();
        }

        var product = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == query.ProductId);
        if (product == null) return new StockLedgerReportDto();

        var warehouse = await _context.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == query.WarehouseId);
        if (warehouse == null) return new StockLedgerReportDto();

        if (query.CompanyId.HasValue && query.CompanyId.Value != Guid.Empty &&
            warehouse.CompanyId.HasValue && warehouse.CompanyId.Value != query.CompanyId.Value)
        {
            return new StockLedgerReportDto();
        }

        if (query.BranchId.HasValue && query.BranchId.Value != Guid.Empty &&
            warehouse.BranchId.HasValue && warehouse.BranchId.Value != query.BranchId.Value)
        {
            return new StockLedgerReportDto();
        }

        // 3. Validate Location (Optional - if omitted/empty, covers All Locations across warehouse)
        bool isSpecificLocation = query.WarehouseLocationId.HasValue && query.WarehouseLocationId.Value != Guid.Empty;
        WarehouseLocation? specificLocation = null;
        if (isSpecificLocation)
        {
            specificLocation = await _context.WarehouseLocations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == query.WarehouseLocationId!.Value && l.WarehouseId == warehouse.WarehouseId);
            if (specificLocation == null) return new StockLedgerReportDto();
        }

        // Hierarchy path & Report Location details
        string hierarchyPath;
        string reportLocationCode;
        string reportLocationName;

        if (specificLocation != null)
        {
            string zoneName = string.Empty;
            if (specificLocation.WarehouseZoneId.HasValue)
            {
                var zone = await _context.WarehouseZones.AsNoTracking()
                    .FirstOrDefaultAsync(z => z.WarehouseZoneId == specificLocation.WarehouseZoneId.Value);
                zoneName = zone?.ZoneName ?? string.Empty;
            }
            hierarchyPath = string.IsNullOrWhiteSpace(zoneName)
                ? $"{warehouse.WarehouseCode} > {specificLocation.LocationCode}"
                : $"{warehouse.WarehouseCode} > {zoneName} > {specificLocation.LocationCode}";
            reportLocationCode = specificLocation.LocationCode;
            reportLocationName = specificLocation.LocationName;
        }
        else
        {
            hierarchyPath = $"{warehouse.WarehouseCode} > All Locations";
            reportLocationCode = "ALL";
            reportLocationName = "All Locations";
        }

        string currency = "LKR";
        if (warehouse.CompanyId.HasValue)
        {
            var company = await _context.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == warehouse.CompanyId.Value);
            if (company != null && !string.IsNullOrWhiteSpace(company.BaseCurrencyCode))
            {
                currency = company.BaseCurrencyCode;
            }
        }

        // Cache warehouse locations for location display
        var warehouseLocations = await _context.WarehouseLocations.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.WarehouseId == warehouse.WarehouseId)
            .ToDictionaryAsync(l => l.WarehouseLocationId);

        // 4. Load all transactions for this Product (at specific Location or across All Locations in Warehouse)
        var txnQuery = _context.InventoryTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.ProductId == query.ProductId
                     && t.WarehouseId == query.WarehouseId);

        if (isSpecificLocation)
        {
            txnQuery = txnQuery.Where(t => t.WarehouseLocationId == query.WarehouseLocationId!.Value);
        }

        var allTxns = await txnQuery.ToListAsync();

        var openingTxns = query.DateFrom.HasValue
            ? allTxns.Where(t => t.TransactionDate < query.DateFrom.Value).ToList()
            : new List<InventoryTransaction>();

        var periodTxns = query.DateFrom.HasValue
            ? allTxns.Where(t => t.TransactionDate >= query.DateFrom.Value).ToList()
            : allTxns;

        if (query.DateTo.HasValue)
        {
            periodTxns = periodTxns.Where(t => t.TransactionDate <= query.DateTo.Value).ToList();
        }

        decimal openingQuantity = openingTxns.Sum(t => t.Quantity);
        decimal openingValue = openingTxns.Sum(t => t.Quantity > 0 ? t.TotalCost : -t.TotalCost);

        var orderedPeriodTxns = periodTxns
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.InventoryTransactionId)
            .ToList();

        var putawayIds = orderedPeriodTxns
            .Where(t => t.MovementType == InventoryMovementType.InternalTransferIn || t.MovementType == InventoryMovementType.InternalTransferOut)
            .Select(t => t.SourceDocumentId)
            .Distinct()
            .ToList();

        var putaways = await _context.Putaways.AsNoTracking()
            .Where(p => putawayIds.Contains(p.PutawayId))
            .ToDictionaryAsync(p => p.PutawayId, p => p.PutawayNumber);

        var grnIds = orderedPeriodTxns
            .Where(t => t.MovementType == InventoryMovementType.GoodsReceipt)
            .Select(t => t.SourceDocumentId)
            .Distinct()
            .ToList();

        var grns = await _context.GoodsReceiptNotes.AsNoTracking()
            .Where(g => grnIds.Contains(g.GoodsReceiptNoteId))
            .ToDictionaryAsync(g => g.GoodsReceiptNoteId, g => g.GrnNumber);

        var shipmentIds = orderedPeriodTxns
            .Where(t => t.MovementType == InventoryMovementType.ShipmentDispatch)
            .Select(t => t.SourceDocumentId)
            .Distinct()
            .ToList();

        var shipments = await _context.SalesShipments.AsNoTracking()
            .Where(s => shipmentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.ShipmentNumber);

        var transferLineIds = orderedPeriodTxns
            .Where(t => (t.MovementType == InventoryMovementType.InternalTransferIn || t.MovementType == InventoryMovementType.InternalTransferOut) && t.SourceDocumentLineId != Guid.Empty)
            .Select(t => t.SourceDocumentLineId)
            .Distinct()
            .ToList();

        var counterpartTxns = transferLineIds.Count > 0
            ? await _context.InventoryTransactions.AsNoTracking()
                .Where(t => t.TenantId == tenantId && transferLineIds.Contains(t.SourceDocumentLineId))
                .ToListAsync()
            : new List<InventoryTransaction>();

        var counterpartLocationIds = counterpartTxns.Select(c => c.WarehouseLocationId).Distinct().ToList();
        var locDict = warehouseLocations.ToDictionary(l => l.Key, l => l.Value.LocationCode);
        var missingLocIds = counterpartLocationIds.Where(id => !locDict.ContainsKey(id)).ToList();
        if (missingLocIds.Count > 0)
        {
            var extraLocs = await _context.WarehouseLocations.AsNoTracking()
                .Where(l => missingLocIds.Contains(l.WarehouseLocationId))
                .ToDictionaryAsync(l => l.WarehouseLocationId, l => l.LocationCode);
            foreach (var kvp in extraLocs)
            {
                locDict[kvp.Key] = kvp.Value;
            }
        }

        decimal runningQty = openingQuantity;
        decimal runningVal = openingValue;
        decimal totalQtyIn = 0;
        decimal totalInwardVal = 0;
        decimal totalQtyOut = 0;
        decimal totalOutwardVal = 0;

        var allRows = new List<StockLedgerRowDto>();

        foreach (var t in orderedPeriodTxns)
        {
            decimal qtyIn = t.Quantity > 0 ? t.Quantity : 0;
            decimal qtyOut = t.Quantity < 0 ? Math.Abs(t.Quantity) : 0;
            decimal costRate = t.UnitCost;
            decimal inwardVal = qtyIn > 0 ? t.TotalCost : 0;
            decimal outwardVal = qtyOut > 0 ? t.TotalCost : 0;

            runningQty += (qtyIn - qtyOut);
            runningVal += (inwardVal - outwardVal);

            totalQtyIn += qtyIn;
            totalInwardVal += inwardVal;
            totalQtyOut += qtyOut;
            totalOutwardVal += outwardVal;

            string docRef = t.TransactionNumber;
            if (putaways.TryGetValue(t.SourceDocumentId, out var putawayNum)) docRef = putawayNum;
            else if (grns.TryGetValue(t.SourceDocumentId, out var grnNum)) docRef = grnNum;
            else if (shipments.TryGetValue(t.SourceDocumentId, out var shipNum)) docRef = shipNum;

            // Resolve current location of this transaction
            string rowLocCode = warehouseLocations.TryGetValue(t.WarehouseLocationId, out var rowLoc)
                ? rowLoc.LocationCode
                : (specificLocation?.LocationCode ?? "—");
            string rowLocName = rowLoc?.LocationName ?? specificLocation?.LocationName ?? string.Empty;

            string? srcLocCode = null;
            string? dstLocCode = null;

            if (t.MovementType == InventoryMovementType.InternalTransferIn)
            {
                dstLocCode = rowLocCode;
                var partner = counterpartTxns.FirstOrDefault(c => c.SourceDocumentLineId == t.SourceDocumentLineId && c.MovementType == InventoryMovementType.InternalTransferOut);
                if (partner != null && locDict.TryGetValue(partner.WarehouseLocationId, out var pCode))
                {
                    srcLocCode = pCode;
                }
            }
            else if (t.MovementType == InventoryMovementType.InternalTransferOut)
            {
                srcLocCode = rowLocCode;
                var partner = counterpartTxns.FirstOrDefault(c => c.SourceDocumentLineId == t.SourceDocumentLineId && c.MovementType == InventoryMovementType.InternalTransferIn);
                if (partner != null && locDict.TryGetValue(partner.WarehouseLocationId, out var pCode))
                {
                    dstLocCode = pCode;
                }
            }
            else if (t.MovementType == InventoryMovementType.GoodsReceipt)
            {
                srcLocCode = "Supplier";
                dstLocCode = rowLocCode;
            }
            else if (t.MovementType == InventoryMovementType.ShipmentDispatch)
            {
                srcLocCode = rowLocCode;
                dstLocCode = "Customer";
            }
            else if (t.Quantity > 0)
            {
                dstLocCode = rowLocCode;
            }
            else
            {
                srcLocCode = rowLocCode;
            }

            string friendlyType = t.MovementType switch
            {
                InventoryMovementType.GoodsReceipt => "Goods Receipt (GRN)",
                InventoryMovementType.SupplierReturn => "Supplier Return",
                InventoryMovementType.InternalTransferIn => putaways.ContainsKey(t.SourceDocumentId) ? "Put Away In" : "Transfer In",
                InventoryMovementType.InternalTransferOut => putaways.ContainsKey(t.SourceDocumentId) ? "Put Away Out" : "Transfer Out",
                InventoryMovementType.StockAdjustmentIn => "Stock Adjustment (+)",
                InventoryMovementType.StockAdjustmentOut => "Stock Adjustment (-)",
                InventoryMovementType.ProductionReceipt => "Production Receipt",
                InventoryMovementType.ProductionIssue => "Production Issue",
                InventoryMovementType.ShipmentDispatch => "Sales Dispatch",
                InventoryMovementType.Reversal => "Reversal",
                _ => t.MovementType.ToString()
            };

            var row = new StockLedgerRowDto
            {
                InventoryTransactionId = t.InventoryTransactionId,
                TransactionDate = t.TransactionDate,
                PostingDate = t.CreatedAt,
                TransactionType = friendlyType,
                MovementTypeCode = (int)t.MovementType,
                DocumentReference = docRef,
                SourceDocumentType = t.SourceDocumentType.ToString(),
                SourceDocumentId = t.SourceDocumentId,
                SourceDocumentLineId = t.SourceDocumentLineId != Guid.Empty ? t.SourceDocumentLineId : null,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                UnitOfMeasure = product.UnitOfMeasure,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseName = warehouse.WarehouseName,
                LocationCode = rowLocCode,
                LocationName = rowLocName,
                SourceLocationCode = srcLocCode,
                DestinationLocationCode = dstLocCode,
                BatchNumber = t.BatchNumber,
                SerialNumber = t.SerialNumber,
                QuantityIn = qtyIn,
                QuantityOut = qtyOut,
                CostRate = costRate,
                InwardValue = inwardVal,
                OutwardValue = outwardVal,
                RunningQuantity = runningQty,
                RunningValue = Math.Round(runningVal, 2, MidpointRounding.AwayFromZero),
                ValuationMethod = product.ValuationMethod.ToString(),
                Currency = currency
            };

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string term = query.Search.Trim().ToLowerInvariant();
                bool matches = row.DocumentReference.ToLowerInvariant().Contains(term) ||
                               row.TransactionType.ToLowerInvariant().Contains(term) ||
                               (row.BatchNumber != null && row.BatchNumber.ToLowerInvariant().Contains(term)) ||
                               (row.SerialNumber != null && row.SerialNumber.ToLowerInvariant().Contains(term)) ||
                               (row.SourceLocationCode != null && row.SourceLocationCode.ToLowerInvariant().Contains(term)) ||
                               (row.DestinationLocationCode != null && row.DestinationLocationCode.ToLowerInvariant().Contains(term));
                if (!matches) continue;
            }

            allRows.Add(row);
        }

        int totalCount = allRows.Count;
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 50 : query.PageSize;

        var pagedItems = allRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new StockLedgerReportDto
        {
            ProductId = product.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            UnitOfMeasure = product.UnitOfMeasure,
            WarehouseId = warehouse.WarehouseId,
            WarehouseCode = warehouse.WarehouseCode,
            WarehouseName = warehouse.WarehouseName,
            WarehouseLocationId = specificLocation?.WarehouseLocationId,
            LocationCode = reportLocationCode,
            LocationName = reportLocationName,
            HierarchyPath = hierarchyPath,
            ValuationMethod = product.ValuationMethod.ToString(),
            Currency = currency,
            OpeningQuantity = openingQuantity,
            OpeningValue = Math.Round(openingValue, 2, MidpointRounding.AwayFromZero),
            TotalQuantityIn = totalQtyIn,
            TotalInwardValue = Math.Round(totalInwardVal, 2, MidpointRounding.AwayFromZero),
            TotalQuantityOut = totalQtyOut,
            TotalOutwardValue = Math.Round(totalOutwardVal, 2, MidpointRounding.AwayFromZero),
            ClosingQuantity = runningQty,
            ClosingValue = Math.Round(runningVal, 2, MidpointRounding.AwayFromZero),
            Items = pagedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
