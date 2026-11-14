using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;

namespace ERP.Application.Features.Inventory.Management;

public class InventoryService : IInventoryService
{
    private readonly IInventoryPersistenceCoordinator _persistenceCoordinator;
    private readonly IInventoryPostingService _postingService;
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(
        IInventoryPersistenceCoordinator persistenceCoordinator,
        IInventoryPostingService postingService,
        IInventoryRepository inventoryRepository)
    {
        _persistenceCoordinator = persistenceCoordinator ?? throw new ArgumentNullException(nameof(persistenceCoordinator));
        _postingService = postingService ?? throw new ArgumentNullException(nameof(postingService));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(Guid tenantId)
    {
        var data = await _persistenceCoordinator.GetSummaryDataAsync(tenantId);
        return new InventorySummaryDto
        {
            TotalStockItems = data.TotalStockItems,
            TotalWarehouses = data.TotalWarehouses,
            TotalLocations = data.TotalLocations,
            LowStockItems = data.LowStockItems,
            TotalTransactions = data.TotalTransactions,
            TotalOnHandQuantity = data.TotalOnHandQuantity,
            TotalInventoryValue = data.TotalInventoryValue
        };
    }

    public async Task<List<InventoryRecentTransactionDto>> GetRecentTransactionsAsync(Guid tenantId, int limit = 10)
    {
        return await _persistenceCoordinator.GetRecentTransactionsAsync(tenantId, limit);
    }

    public async Task<List<StockIssueDto>> GetStockIssuesAsync(Guid tenantId, Guid? warehouseId = null, StockIssuePurpose? purpose = null)
    {
        return await _persistenceCoordinator.GetStockIssuesAsync(tenantId, warehouseId, purpose);
    }

    public async Task<StockIssueDto?> GetStockIssueByIdAsync(Guid tenantId, Guid issueId)
    {
        return await _persistenceCoordinator.GetStockIssueByIdAsync(tenantId, issueId);
    }

    public async Task<AvailableStockDto> GetAvailableStockAsync(
        Guid tenantId,
        Guid productId,
        Guid locationId,
        string? batchNumber = null,
        string? serialNumber = null)
    {
        var balance = await _inventoryRepository.GetBalanceAsync(
            tenantId,
            locationId,
            productId,
            batchNumber ?? string.Empty,
            serialNumber ?? string.Empty);

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

        var warehouse = await _persistenceCoordinator.GetWarehouseAsync(tenantId, request.WarehouseId);
        if (warehouse == null || !warehouse.IsActive)
            throw new KeyNotFoundException("Warehouse not found or is inactive.");

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
            bool alreadyPosted = await _persistenceCoordinator.HasTransactionsBySourceAsync(tenantId, sourceDocType, issueBatchId);
            if (alreadyPosted)
            {
                throw new InvalidOperationException($"Stock issue '{issueBatchId}' has already been posted.");
            }
        }

        // Validate line inputs and collect entity metadata
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = request.Lines.Select(l => l.WarehouseLocationId).Distinct().ToList();

        var products = await _persistenceCoordinator.GetActiveProductsByIdsAsync(tenantId, productIds);
        var locations = await _persistenceCoordinator.GetActiveLocationsByIdsAsync(tenantId, locationIds);

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

            if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.BatchNumber))
                {
                    throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                }
            }

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

        return await _persistenceCoordinator.ExecuteStrategyAsync(async () =>
        {
            var dbTransaction = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
            try
            {
                var results = new List<StockIssueDto>();
                int sequence = 1;

                foreach (var line in request.Lines)
                {
                    Guid lineId = line.StockIssueLineId ?? DeriveDeterministicGuid(tenantId, issueBatchId.ToString(), $"line_{sequence}");
                    var product = products[line.ProductId];
                    var location = locations[line.WarehouseLocationId];

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
                        BatchNumber = line.BatchNumber?.Trim(),
                        SerialNumber = line.SerialNumber?.Trim(),
                        TransactionDate = request.IssueDate ?? DateTime.UtcNow
                    };

                    var tx = await _postingService.PostMovementAsync(tenantId, postRequest);

                    results.Add(new StockIssueDto
                    {
                        StockIssueId = tx.InventoryTransactionId,
                        StockIssueLineId = lineId,
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

                await _persistenceCoordinator.CommitTransactionIfNeededAsync(dbTransaction);

                return results;
            }
            catch (Exception)
            {
                await _persistenceCoordinator.RollbackTransactionIfNeededAsync(dbTransaction);
                throw;
            }
        });
    }

    public async Task<List<StockReceiptDto>> GetStockReceiptsAsync(Guid tenantId, Guid? warehouseId = null, StockReceiptReason? reason = null)
    {
        return await _persistenceCoordinator.GetStockReceiptsAsync(tenantId, warehouseId, reason);
    }

    public async Task<StockReceiptDto?> GetStockReceiptByIdAsync(Guid tenantId, Guid receiptId)
    {
        return await _persistenceCoordinator.GetStockReceiptByIdAsync(tenantId, receiptId);
    }

    public async Task<StockByLocationReportDto> GetStockByLocationReportAsync(Guid tenantId, StockByLocationReportQuery query)
    {
        return await _persistenceCoordinator.GetStockByLocationReportAsync(tenantId, query);
    }

    public async Task<StockLedgerReportDto> GetStockLedgerReportAsync(Guid tenantId, StockLedgerReportQuery query)
    {
        return await _persistenceCoordinator.GetStockLedgerReportAsync(tenantId, query);
    }

    public async Task<List<StockReceiptDto>> CreateStockReceiptAsync(Guid tenantId, CreateStockReceiptRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.WarehouseId == Guid.Empty)
            throw new ArgumentException("Warehouse is required.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one line item is required to receive stock.");

        var warehouse = await _persistenceCoordinator.GetWarehouseAsync(tenantId, request.WarehouseId);
        if (warehouse == null || !warehouse.IsActive)
            throw new KeyNotFoundException($"Warehouse '{request.WarehouseId}' not found or is inactive.");

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

        if (request.StockReceiptId.HasValue || !string.IsNullOrWhiteSpace(request.SourceDocumentReference))
        {
            bool alreadyPosted = await _persistenceCoordinator.HasTransactionsBySourceAsync(tenantId, sourceDocType, receiptBatchId);
            if (alreadyPosted)
            {
                throw new InvalidOperationException($"Stock receipt '{receiptBatchId}' has already been posted.");
            }
        }

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var locationIds = request.Lines.Select(l => l.WarehouseLocationId).Distinct().ToList();

        var products = await _persistenceCoordinator.GetActiveProductsByIdsAsync(tenantId, productIds);
        var locations = await _persistenceCoordinator.GetActiveLocationsByIdsAsync(tenantId, locationIds);

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

            if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
            {
                if (string.IsNullOrWhiteSpace(line.BatchNumber))
                {
                    throw new InvalidOperationException($"Batch number is required for batch-tracked product '{product.ProductCode}'.");
                }
            }

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

            if (line.ManufacturingDate.HasValue && line.ExpiryDate.HasValue && line.ExpiryDate.Value.Date < line.ManufacturingDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Expiry date ({line.ExpiryDate.Value:yyyy-MM-dd}) cannot be earlier than manufacturing date ({line.ManufacturingDate.Value:yyyy-MM-dd}).");
            }
        }

        return await _persistenceCoordinator.ExecuteStrategyAsync(async () =>
        {
            var dbTransaction = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
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

                await _persistenceCoordinator.CommitTransactionIfNeededAsync(dbTransaction);

                return results;
            }
            catch (Exception)
            {
                await _persistenceCoordinator.RollbackTransactionIfNeededAsync(dbTransaction);
                throw;
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

    private static Guid DeriveDeterministicGuid(Guid tenantId, string scope, string identifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes($"{tenantId:D}:{scope}:{identifier}");
        var hash = sha256.ComputeHash(inputBytes);
        return new Guid(hash.AsSpan(0, 16));
    }
}
