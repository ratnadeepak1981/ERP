using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory;

public class InventoryService : IInventoryService
{
    private readonly DomainDbContext _context;
    private readonly IInventoryPostingService _postingService;

    public InventoryService(DomainDbContext context, IInventoryPostingService postingService)
    {
        _context = context;
        _postingService = postingService;
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
                StockIssuePurpose.InternalUse => query.Where(t => t.MovementType == InventoryMovementType.StockAdjustmentOut),
                StockIssuePurpose.Other => query.Where(t => t.MovementType != InventoryMovementType.ShipmentDispatch &&
                                                            t.MovementType != InventoryMovementType.ProductionIssue &&
                                                            t.MovementType != InventoryMovementType.StockAdjustmentOut),
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

            var issuePurpose = MapMovementTypeToPurpose(t.MovementType);

            return new StockIssueDto
            {
                StockIssueId = t.InventoryTransactionId,
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

        var purpose = MapMovementTypeToPurpose(tx.MovementType);

        return new StockIssueDto
        {
            StockIssueId = tx.InventoryTransactionId,
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

        Guid issueBatchId = Guid.NewGuid();

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

        var results = new List<StockIssueDto>();
        int sequence = 1;

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Issue quantity must be greater than zero for all lines.");
            if (line.ProductId == Guid.Empty)
                throw new ArgumentException("Product is required for each line.");
            if (line.WarehouseLocationId == Guid.Empty)
                throw new ArgumentException("Warehouse storage location is required for each line.");

            var product = await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == line.ProductId && p.IsActive)
                ?? throw new KeyNotFoundException($"Product '{line.ProductId}' not found or is inactive.");

            if (!product.IsStockTracked)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not a stock-tracked item.");

            var location = await _context.WarehouseLocations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == line.WarehouseLocationId && l.IsActive)
                ?? throw new KeyNotFoundException($"Storage location '{line.WarehouseLocationId}' not found or is inactive.");

            if (location.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException($"Location '{location.LocationCode}' does not belong to warehouse '{warehouse.WarehouseCode}'.");

            // Validate available stock before posting
            var balance = await _postingService.GetBalanceAsync(
                tenantId,
                line.WarehouseLocationId,
                line.ProductId,
                line.BatchNumber,
                line.SerialNumber);

            decimal available = balance?.QuantityAvailable ?? 0m;
            if (line.Quantity > available)
            {
                throw new InvalidOperationException(
                    $"Insufficient available stock for '{product.ProductCode} - {product.ProductName}' at location '{location.LocationCode}'. Available: {available}, Requested: {line.Quantity}");
            }

            var postRequest = new PostInventoryMovementRequest
            {
                MovementType = movementType,
                SourceDocumentType = sourceDocType,
                SourceDocumentId = issueBatchId,
                SourceDocumentLineId = Guid.NewGuid(),
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

            results.Add(new StockIssueDto
            {
                StockIssueId = tx.InventoryTransactionId,
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

        return results;
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

        return transactions.Select(t =>
        {
            products.TryGetValue(t.ProductId, out var product);
            warehouses.TryGetValue(t.WarehouseId, out var warehouse);
            locations.TryGetValue(t.WarehouseLocationId, out var location);

            var receiptReason = MapMovementTypeToReceiptReason(t.MovementType);

            return new StockReceiptDto
            {
                StockReceiptId = t.InventoryTransactionId,
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

        var receiptReason = MapMovementTypeToReceiptReason(t.MovementType);

        return new StockReceiptDto
        {
            StockReceiptId = t.InventoryTransactionId,
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

        Guid receiptBatchId = Guid.NewGuid();

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

        var results = new List<StockReceiptDto>();
        int sequence = 1;

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Receipt quantity must be greater than zero for all lines.");
            if (line.ProductId == Guid.Empty)
                throw new ArgumentException("Product is required for each line.");
            if (line.WarehouseLocationId == Guid.Empty)
                throw new ArgumentException("Warehouse storage location is required for each line.");

            var product = await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == line.ProductId && p.IsActive)
                ?? throw new KeyNotFoundException($"Product '{line.ProductId}' not found or is inactive.");

            if (!product.IsStockTracked)
                throw new InvalidOperationException($"Product '{product.ProductCode}' is not a stock-tracked item.");

            var location = await _context.WarehouseLocations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == line.WarehouseLocationId && l.IsActive)
                ?? throw new KeyNotFoundException($"Storage location '{line.WarehouseLocationId}' not found or is inactive.");

            if (location.WarehouseId != request.WarehouseId)
                throw new InvalidOperationException($"Location '{location.LocationCode}' does not belong to warehouse '{warehouse.WarehouseCode}'.");

            decimal unitCost = line.UnitCost ?? product.StandardCost;

            var postRequest = new PostInventoryMovementRequest
            {
                MovementType = movementType,
                SourceDocumentType = sourceDocType,
                SourceDocumentId = receiptBatchId,
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = sequence++,
                ProductId = line.ProductId,
                WarehouseId = request.WarehouseId,
                WarehouseLocationId = line.WarehouseLocationId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                BatchNumber = line.BatchNumber,
                SerialNumber = line.SerialNumber,
                TransactionDate = request.ReceiptDate ?? DateTime.UtcNow
            };

            var tx = await _postingService.PostMovementAsync(tenantId, postRequest);

            results.Add(new StockReceiptDto
            {
                StockReceiptId = tx.InventoryTransactionId,
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
                SourceDocumentReference = request.SourceDocumentReference,
                Remarks = line.Remarks ?? request.Remarks,
                CreatedAt = tx.CreatedAt
            });
        }

        return results;
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

    private static StockIssuePurpose MapMovementTypeToPurpose(InventoryMovementType type) => type switch
    {
        InventoryMovementType.ShipmentDispatch => StockIssuePurpose.SalesDispatch,
        InventoryMovementType.ProductionIssue => StockIssuePurpose.Manufacturing,
        InventoryMovementType.StockAdjustmentOut => StockIssuePurpose.InternalUse,
        _ => StockIssuePurpose.Other
    };

    private static StockReceiptReason MapMovementTypeToReceiptReason(InventoryMovementType type) => type switch
    {
        InventoryMovementType.ProductionReceipt => StockReceiptReason.InternalProduction,
        _ => StockReceiptReason.OpeningBalance
    };
}
