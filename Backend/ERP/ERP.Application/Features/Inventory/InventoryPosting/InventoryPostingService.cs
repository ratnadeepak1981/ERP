using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Warehouse;

namespace ERP.Application.Features.Inventory.InventoryPosting;

public class InventoryPostingService : IInventoryPostingService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryValuationService _valuationService;
    private readonly IInventoryPersistenceCoordinator _persistenceCoordinator;

    public InventoryPostingService(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryValuationService valuationService,
        IInventoryPersistenceCoordinator persistenceCoordinator)
    {
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _valuationService = valuationService;
        _persistenceCoordinator = persistenceCoordinator;
    }

    public async Task<InventoryTransaction> PostMovementAsync(Guid tenantId, PostInventoryMovementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.Quantity == 0)
            throw new ArgumentException("Quantity cannot be zero.", nameof(request.Quantity));

        // 1. Validate Product
        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new KeyNotFoundException("Product not found or does not belong to the current tenant.");

        if (!product.IsStockTracked)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not configured as a stock-tracked item.");

        ValidateTrackingMode(product, request.BatchNumber, request.SerialNumber, request.Quantity);

        // 2. Validate Warehouse and Location
        var warehouse = await _warehouseRepository.GetWarehouseByIdAsync(tenantId, request.WarehouseId)
            ?? throw new KeyNotFoundException("Warehouse not found or does not belong to the current tenant.");

        var location = await _warehouseRepository.GetLocationByIdAsync(tenantId, request.WarehouseLocationId)
            ?? throw new KeyNotFoundException("Location not found or does not belong to the current tenant.");

        if (location.WarehouseId != request.WarehouseId)
            throw new InvalidOperationException("Location does not belong to the specified warehouse.");

        if (!location.IsActive)
            throw new InvalidOperationException("Cannot post inventory to an inactive location.");

        if (location.LocationTypeId.HasValue)
        {
            var locType = await _warehouseRepository.GetLocationTypeByIdAsync(tenantId, location.LocationTypeId.Value);
            if (locType != null && !locType.CanStoreInventory)
            {
                throw new InvalidOperationException($"Location '{location.LocationCode}' has location type '{locType.Code}' which does not allow storing inventory.");
            }
        }

        // 3. Idempotency Check
        Guid lineId = request.SourceDocumentLineId ?? Guid.Empty;
        if (await _inventoryRepository.TransactionExistsByIdempotencyKeyAsync(
            tenantId,
            request.SourceDocumentType,
            request.SourceDocumentId,
            lineId,
            request.MovementType,
            request.MovementSequence))
        {
            throw new InvalidOperationException("Duplicate inventory movement posting detected for this source document line and sequence.");
        }

        // 4. Begin Execution within DB Transaction (participates in outer ambient transaction if already begun)
        var dbTransaction = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
        try
        {
            // Acquire transactional valuation lock for this SKU + Warehouse
            await _valuationService.AcquireValuationLockAsync(tenantId, request.ProductId, request.WarehouseId);

            string normBatch = request.BatchNumber?.Trim() ?? string.Empty;
            string normSerial = request.SerialNumber?.Trim() ?? string.Empty;

            decimal signedQuantity = GetSignedQuantity(request.MovementType, request.Quantity);

            // Authoritative Batch/Lot validation and synchronization
            await ValidateAndSynchronizeBatchAsync(tenantId, product, normBatch, request, signedQuantity > 0);

            // Fetch or create balance
            var balance = await _inventoryRepository.GetBalanceAsync(
                tenantId,
                request.WarehouseLocationId,
                request.ProductId,
                normBatch,
                normSerial);

            if (balance == null)
            {
                balance = InventoryBalance.Create(
                    tenantId,
                    request.WarehouseId,
                    request.WarehouseLocationId,
                    request.ProductId,
                    normBatch,
                    normSerial);
                await _inventoryRepository.AddBalanceAsync(balance);
            }

            // Apply Balance Changes
            if (signedQuantity < 0)
            {
                // Outflow: Ensure location allows picking
                if (location.LocationTypeId.HasValue)
                {
                    var locType = await _warehouseRepository.GetLocationTypeByIdAsync(tenantId, location.LocationTypeId.Value);
                    if (locType != null && !locType.CanPick)
                    {
                        throw new InvalidOperationException(
                            $"Location '{location.LocationCode}' does not allow picking for stock outflow (type '{locType.Name}' has picking disabled).");
                    }
                }

                // Outflow: Ensure sufficient available stock
                decimal requestedOutflow = Math.Abs(signedQuantity);
                if (requestedOutflow > balance.QuantityAvailable)
                {
                    throw new InvalidOperationException(
                        $"Insufficient available inventory at location '{location.LocationCode}'. Requested: {requestedOutflow}, Available: {balance.QuantityAvailable} (On-Hand: {balance.QuantityOnHand}, Quarantined: {balance.QuantityQuarantine}, Allocated: {balance.QuantityAllocated}).");
                }
            }

            Guid txnId = Guid.NewGuid();

            var valContext = new ValuationContext
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                WarehouseLocationId = request.WarehouseLocationId,
                Product = product,
                BatchNumber = normBatch,
                SerialNumber = normSerial,
                TransactionDate = request.TransactionDate ?? DateTime.UtcNow
            };

            decimal effectiveUnitCost;
            if (signedQuantity > 0)
            {
                var inflowResult = await _valuationService.ProcessInflowAsync(
                    valContext,
                    request.Quantity,
                    request.UnitCost,
                    txnId);
                effectiveUnitCost = inflowResult.UnitCost;
            }
            else
            {
                var outflowResult = await _valuationService.ProcessOutflowAsync(
                    valContext,
                    Math.Abs(signedQuantity),
                    txnId);
                effectiveUnitCost = outflowResult.UnitCost;
            }

            balance.ApplyOnHandChange(signedQuantity);

            if (request.IsQuarantine)
            {
                balance.ApplyQuarantineChange(signedQuantity);
            }

            string txnNumber = GenerateTransactionNumber();

            var txn = InventoryTransaction.Create(
                tenantId,
                txnNumber,
                request.MovementType,
                request.SourceDocumentType,
                request.SourceDocumentId,
                lineId,
                request.MovementSequence,
                request.ProductId,
                request.WarehouseId,
                request.WarehouseLocationId,
                signedQuantity,
                effectiveUnitCost,
                request.UnitOfMeasureId ?? product.UnitOfMeasureId,
                normBatch,
                normSerial,
                request.ExpiryDate,
                request.TransactionDate ?? DateTime.UtcNow,
                inventoryTransactionId: txnId);

            await _inventoryRepository.AddTransactionAsync(txn);
            await _inventoryRepository.SaveChangesAsync();

            await _persistenceCoordinator.CommitTransactionIfNeededAsync(dbTransaction);

            return txn;
        }
        catch (Exception)
        {
            await _persistenceCoordinator.RollbackTransactionIfNeededAsync(dbTransaction);
            throw;
        }
    }

    public async Task<(InventoryTransaction Outflow, InventoryTransaction Inflow)> PostTransferAsync(
        Guid tenantId,
        PostTransferMovementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.Quantity <= 0)
            throw new ArgumentException("Transfer quantity must be greater than zero.", nameof(request.Quantity));

        if (request.SourceLocationId == request.DestinationLocationId)
            throw new InvalidOperationException("Source and destination locations cannot be identical.");

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new KeyNotFoundException("Product not found or does not belong to the current tenant.");

        if (!product.IsStockTracked)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is not configured as a stock-tracked item.");

        ValidateTrackingMode(product, request.BatchNumber, request.SerialNumber, request.Quantity);

        // Validate locations
        var srcLocation = await _warehouseRepository.GetLocationByIdAsync(tenantId, request.SourceLocationId)
            ?? throw new KeyNotFoundException("Source warehouse location not found.");

        var dstLocation = await _warehouseRepository.GetLocationByIdAsync(tenantId, request.DestinationLocationId)
            ?? throw new KeyNotFoundException("Destination warehouse location not found.");

        if (dstLocation.LocationTypeId.HasValue)
        {
            var dstLocType = await _warehouseRepository.GetLocationTypeByIdAsync(tenantId, dstLocation.LocationTypeId.Value);
            if (dstLocType != null && !dstLocType.CanStoreInventory)
                throw new InvalidOperationException($"Destination location '{dstLocation.LocationCode}' cannot store inventory.");
        }

        Guid lineId = request.SourceDocumentLineId ?? Guid.Empty;

        // Idempotency check for transfer
        if (await _inventoryRepository.TransactionExistsByIdempotencyKeyAsync(
            tenantId,
            request.SourceDocumentType,
            request.SourceDocumentId,
            lineId,
            InventoryMovementType.InternalTransferOut,
            1))
        {
            throw new InvalidOperationException("Duplicate transfer movement posting detected.");
        }

        var dbTransaction = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
        try
        {
            // Acquire valuation locks in deterministic order across source & destination warehouses
            await _valuationService.AcquireTransferValuationLocksAsync(
                tenantId,
                request.ProductId,
                request.SourceWarehouseId,
                request.DestinationWarehouseId);

            string normBatch = request.BatchNumber?.Trim() ?? string.Empty;
            string normSerial = request.SerialNumber?.Trim() ?? string.Empty;

            // Authoritative Batch/Lot validation for transfer
            await ValidateTransferBatchAsync(tenantId, product, normBatch, request);

            // Source balance
            var srcBalance = await _inventoryRepository.GetBalanceAsync(
                tenantId,
                request.SourceLocationId,
                request.ProductId,
                normBatch,
                normSerial);

            if (srcBalance == null)
            {
                throw new InvalidOperationException($"No stock exists at source location '{srcLocation.LocationCode}'.");
            }

            if (request.IsSourceQuarantine)
            {
                if (request.Quantity > srcBalance.QuantityQuarantine)
                {
                    throw new InvalidOperationException($"Insufficient quarantine stock at source location. Requested: {request.Quantity}, Quarantine available: {srcBalance.QuantityQuarantine}.");
                }

                srcBalance.ApplyOnHandChange(-request.Quantity);
                srcBalance.ApplyQuarantineChange(-request.Quantity);
            }
            else
            {
                if (request.Quantity > srcBalance.QuantityAvailable)
                {
                    throw new InvalidOperationException($"Insufficient available stock at source location. Requested: {request.Quantity}, Available: {srcBalance.QuantityAvailable}.");
                }

                srcBalance.ApplyOnHandChange(-request.Quantity);
            }

            // Destination balance
            var dstBalance = await _inventoryRepository.GetBalanceAsync(
                tenantId,
                request.DestinationLocationId,
                request.ProductId,
                normBatch,
                normSerial);

            if (dstBalance == null)
            {
                dstBalance = InventoryBalance.Create(
                    tenantId,
                    request.DestinationWarehouseId,
                    request.DestinationLocationId,
                    request.ProductId,
                    normBatch,
                    normSerial);
                await _inventoryRepository.AddBalanceAsync(dstBalance);
            }

            dstBalance.ApplyOnHandChange(request.Quantity);
            if (request.IsDestinationQuarantine)
            {
                dstBalance.ApplyQuarantineChange(request.Quantity);
            }

            Guid outTxnId = Guid.NewGuid();
            Guid inTxnId = Guid.NewGuid();

            // Valuation: Relieve source and establish destination
            var srcValContext = new ValuationContext
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.SourceWarehouseId,
                WarehouseLocationId = request.SourceLocationId,
                Product = product,
                BatchNumber = normBatch,
                SerialNumber = normSerial,
                TransactionDate = request.TransactionDate ?? DateTime.UtcNow
            };
            var outflowResult = await _valuationService.ProcessOutflowAsync(srcValContext, request.Quantity, outTxnId);
            decimal transferUnitCost = outflowResult.UnitCost;

            var dstValContext = new ValuationContext
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.DestinationWarehouseId,
                WarehouseLocationId = request.DestinationLocationId,
                Product = product,
                BatchNumber = normBatch,
                SerialNumber = normSerial,
                TransactionDate = request.TransactionDate ?? DateTime.UtcNow
            };
            await _valuationService.ProcessInflowAsync(dstValContext, request.Quantity, transferUnitCost, inTxnId);

            // Outflow transaction
            var outTxn = InventoryTransaction.Create(
                tenantId,
                GenerateTransactionNumber(),
                InventoryMovementType.InternalTransferOut,
                request.SourceDocumentType,
                request.SourceDocumentId,
                lineId,
                1,
                request.ProductId,
                request.SourceWarehouseId,
                request.SourceLocationId,
                -request.Quantity,
                transferUnitCost,
                request.UnitOfMeasureId ?? product.UnitOfMeasureId,
                normBatch,
                normSerial,
                request.ExpiryDate,
                request.TransactionDate ?? DateTime.UtcNow,
                inventoryTransactionId: outTxnId);

            // Inflow transaction
            var inTxn = InventoryTransaction.Create(
                tenantId,
                GenerateTransactionNumber(),
                InventoryMovementType.InternalTransferIn,
                request.SourceDocumentType,
                request.SourceDocumentId,
                lineId,
                2,
                request.ProductId,
                request.DestinationWarehouseId,
                request.DestinationLocationId,
                request.Quantity,
                transferUnitCost,
                request.UnitOfMeasureId ?? product.UnitOfMeasureId,
                normBatch,
                normSerial,
                request.ExpiryDate,
                request.TransactionDate ?? DateTime.UtcNow,
                inventoryTransactionId: inTxnId);

            await _inventoryRepository.AddTransactionAsync(outTxn);
            await _inventoryRepository.AddTransactionAsync(inTxn);
            await _inventoryRepository.SaveChangesAsync();

            await _persistenceCoordinator.CommitTransactionIfNeededAsync(dbTransaction);

            return (outTxn, inTxn);
        }
        catch (Exception)
        {
            await _persistenceCoordinator.RollbackTransactionIfNeededAsync(dbTransaction);
            throw;
        }
    }

    public async Task<InventoryTransaction> PostReversalAsync(Guid tenantId, PostReversalRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var dbTransaction = await _persistenceCoordinator.BeginTransactionIfNeededAsync();
        try
        {
            var originalTxn = await _inventoryRepository.GetTransactionByIdAsync(tenantId, request.OriginalTransactionId)
                ?? throw new KeyNotFoundException("Original inventory transaction not found.");

            if (originalTxn.MovementType == InventoryMovementType.Reversal)
            {
                throw new InvalidOperationException("Cannot reverse a reversal transaction.");
            }

            // Lock the product and warehouse
            await _valuationService.AcquireValuationLockAsync(tenantId, originalTxn.ProductId, originalTxn.WarehouseId);

            var product = await _productRepository.GetByIdAsync(tenantId, originalTxn.ProductId)
                ?? throw new KeyNotFoundException("Product not found.");

            // Find existing reversals for this transaction
            var existingReversals = await _inventoryRepository.GetTransactionsByOriginalIdAsync(tenantId, request.OriginalTransactionId);
            decimal alreadyReversedQty = existingReversals.Sum(r => Math.Abs(r.Quantity));
            decimal origQty = Math.Abs(originalTxn.Quantity);
            decimal remainingQty = origQty - alreadyReversedQty;

            if (remainingQty <= 0)
            {
                throw new InvalidOperationException($"Original transaction '{originalTxn.TransactionNumber}' has already been fully reversed.");
            }

            decimal qtyToReverse = request.QuantityToReverse.HasValue
                ? Math.Abs(request.QuantityToReverse.Value)
                : remainingQty;

            if (qtyToReverse <= 0)
            {
                throw new ArgumentException("Reversal quantity must be greater than zero.", nameof(request.QuantityToReverse));
            }

            if (qtyToReverse > remainingQty)
            {
                throw new InvalidOperationException($"Reversal quantity ({qtyToReverse}) exceeds remaining unreverted quantity ({remainingQty}).");
            }

            decimal reversalSignedQty = originalTxn.Quantity > 0 ? -qtyToReverse : qtyToReverse;

            string normBatch = originalTxn.BatchNumber ?? string.Empty;
            string normSerial = originalTxn.SerialNumber ?? string.Empty;

            var balance = await _inventoryRepository.GetBalanceAsync(
                tenantId,
                originalTxn.WarehouseLocationId,
                originalTxn.ProductId,
                normBatch,
                normSerial);

            if (balance == null)
            {
                balance = InventoryBalance.Create(
                    tenantId,
                    originalTxn.WarehouseId,
                    originalTxn.WarehouseLocationId,
                    originalTxn.ProductId,
                    normBatch,
                    normSerial);
                await _inventoryRepository.AddBalanceAsync(balance);
            }

            if (reversalSignedQty < 0 && Math.Abs(reversalSignedQty) > balance.QuantityAvailable)
            {
                throw new InvalidOperationException($"Cannot reverse transaction: Insufficient available inventory to deduct. Available: {balance.QuantityAvailable}, Requested deduction: {Math.Abs(reversalSignedQty)}.");
            }

            Guid reversalTxnId = Guid.NewGuid();

            var valContext = new ValuationContext
            {
                TenantId = tenantId,
                ProductId = originalTxn.ProductId,
                WarehouseId = originalTxn.WarehouseId,
                WarehouseLocationId = originalTxn.WarehouseLocationId,
                Product = product,
                BatchNumber = normBatch,
                SerialNumber = normSerial,
                TransactionDate = request.TransactionDate ?? DateTime.UtcNow
            };

            decimal effectiveReversalUnitCost;
            if (originalTxn.Quantity > 0)
            {
                var reversalResult = await _valuationService.ProcessInflowReversalAsync(
                    valContext,
                    originalTxn,
                    qtyToReverse,
                    reversalTxnId);
                effectiveReversalUnitCost = reversalResult.ReversalUnitCost;
            }
            else
            {
                var reversalResult = await _valuationService.ProcessOutflowReversalAsync(
                    valContext,
                    originalTxn,
                    qtyToReverse,
                    reversalTxnId);
                effectiveReversalUnitCost = reversalResult.ReversalUnitCost;
            }

            balance.ApplyOnHandChange(reversalSignedQty);

            int seq = existingReversals.Count + 1;
            var reversalTxn = InventoryTransaction.Create(
                tenantId,
                GenerateTransactionNumber(),
                InventoryMovementType.Reversal,
                originalTxn.SourceDocumentType,
                originalTxn.SourceDocumentId,
                originalTxn.SourceDocumentLineId,
                seq,
                originalTxn.ProductId,
                originalTxn.WarehouseId,
                originalTxn.WarehouseLocationId,
                reversalSignedQty,
                effectiveReversalUnitCost,
                originalTxn.UnitOfMeasureId,
                normBatch,
                normSerial,
                originalTxn.ExpiryDate,
                request.TransactionDate ?? DateTime.UtcNow,
                reversalOfTransactionId: originalTxn.InventoryTransactionId,
                inventoryTransactionId: reversalTxnId);

            await _inventoryRepository.AddTransactionAsync(reversalTxn);
            await _inventoryRepository.SaveChangesAsync();

            await _persistenceCoordinator.CommitTransactionIfNeededAsync(dbTransaction);

            return reversalTxn;
        }
        catch (Exception)
        {
            await _persistenceCoordinator.RollbackTransactionIfNeededAsync(dbTransaction);
            throw;
        }
    }

    public async Task<InventoryBalance?> GetBalanceAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        string? batchNumber = null,
        string? serialNumber = null)
    {
        return await _inventoryRepository.GetBalanceAsync(
            tenantId,
            locationId,
            productId,
            batchNumber ?? string.Empty,
            serialNumber ?? string.Empty);
    }

    public async Task<decimal> GetTotalAvailableStockAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        bool isBatchTracked,
        DateTime? asOfDate = null)
    {
        return await _inventoryRepository.GetTotalAvailableStockAsync(
            tenantId,
            locationId,
            productId,
            isBatchTracked,
            asOfDate);
    }

    public async Task<List<InventoryBalance>> GetProductBalancesAsync(Guid tenantId, Guid productId)
    {
        return await _inventoryRepository.GetBalancesByProductAsync(tenantId, productId);
    }

    private static decimal GetSignedQuantity(InventoryMovementType type, decimal quantity)
    {
        decimal absQty = Math.Abs(quantity);
        return type switch
        {
            InventoryMovementType.GoodsReceipt => absQty,
            InventoryMovementType.InternalTransferIn => absQty,
            InventoryMovementType.StockAdjustmentIn => absQty,
            InventoryMovementType.ProductionReceipt => absQty,

            InventoryMovementType.SupplierReturn => -absQty,
            InventoryMovementType.InternalTransferOut => -absQty,
            InventoryMovementType.StockAdjustmentOut => -absQty,
            InventoryMovementType.ProductionIssue => -absQty,
            InventoryMovementType.ShipmentDispatch => -absQty,

            InventoryMovementType.Reversal => quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported movement type {type}")
        };
    }

    private static void ValidateTrackingMode(Product product, string? batchNumber, string? serialNumber, decimal quantity)
    {
        switch (product.TrackingMode)
        {
            case TrackingMode.None:
                if (!string.IsNullOrWhiteSpace(batchNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track batches. BatchNumber must be empty.");
                if (!string.IsNullOrWhiteSpace(serialNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track serial numbers. SerialNumber must be empty.");
                break;

            case TrackingMode.Batch:
                if (string.IsNullOrWhiteSpace(batchNumber))
                    throw new InvalidOperationException($"BatchNumber is required for batch-tracked product '{product.ProductCode}'.");
                if (!string.IsNullOrWhiteSpace(serialNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track serial numbers. SerialNumber must be empty.");
                break;

            case TrackingMode.Serial:
                if (string.IsNullOrWhiteSpace(serialNumber))
                    throw new InvalidOperationException($"SerialNumber is required for serial-tracked product '{product.ProductCode}'.");
                if (Math.Abs(quantity) != 1.0m)
                    throw new InvalidOperationException($"Serial-tracked items must have quantity of 1 (or -1). Specified: {quantity}.");
                if (!string.IsNullOrWhiteSpace(batchNumber))
                    throw new InvalidOperationException($"Product '{product.ProductCode}' does not track batches. BatchNumber must be empty.");
                break;

            case TrackingMode.BatchAndSerial:
                if (string.IsNullOrWhiteSpace(batchNumber))
                    throw new InvalidOperationException($"BatchNumber is required for product '{product.ProductCode}'.");
                if (string.IsNullOrWhiteSpace(serialNumber))
                    throw new InvalidOperationException($"SerialNumber is required for product '{product.ProductCode}'.");
                if (Math.Abs(quantity) != 1.0m)
                    throw new InvalidOperationException($"Serial-tracked items must have quantity of 1 (or -1). Specified: {quantity}.");
                break;
        }
    }

    private async Task ValidateAndSynchronizeBatchAsync(
        Guid tenantId,
        Product product,
        string normBatch,
        PostInventoryMovementRequest request,
        bool isInflow)
    {
        if (product.TrackingMode != TrackingMode.Batch && product.TrackingMode != TrackingMode.BatchAndSerial)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(normBatch))
        {
            throw new InvalidOperationException($"BatchNumber is required for batch-tracked product '{product.ProductCode}'.");
        }

        var batchInTenant = await _inventoryRepository.GetBatchByNumberAsync(tenantId, normBatch);
        if (batchInTenant != null && batchInTenant.ProductId != product.ProductId)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' is already associated with another product in this tenant. A batch number cannot be reused for a different product.");
        }

        var productBatch = await _inventoryRepository.GetBatchAsync(tenantId, product.ProductId, normBatch);

        if (productBatch != null)
        {
            if (request.ExpiryDate.HasValue && productBatch.ExpiryDate.HasValue &&
                request.ExpiryDate.Value.Date != productBatch.ExpiryDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Conflicting expiry date for existing batch '{normBatch}'. Registered: {productBatch.ExpiryDate.Value:yyyy-MM-dd}, Provided: {request.ExpiryDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
            }

            if (request.ManufacturingDate.HasValue && productBatch.ManufacturingDate.HasValue &&
                request.ManufacturingDate.Value.Date != productBatch.ManufacturingDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Conflicting manufacturing date for existing batch '{normBatch}'. Registered: {productBatch.ManufacturingDate.Value:yyyy-MM-dd}, Provided: {request.ManufacturingDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
            }

            if (!productBatch.ExpiryDate.HasValue && request.ExpiryDate.HasValue)
            {
                productBatch.UpdateDates(productBatch.ManufacturingDate ?? request.ManufacturingDate, request.ExpiryDate);
            }
            if (!productBatch.ManufacturingDate.HasValue && request.ManufacturingDate.HasValue)
            {
                productBatch.UpdateDates(request.ManufacturingDate, productBatch.ExpiryDate ?? request.ExpiryDate);
            }

            if (!request.ExpiryDate.HasValue && productBatch.ExpiryDate.HasValue)
            {
                request.ExpiryDate = productBatch.ExpiryDate;
            }
            if (!request.ManufacturingDate.HasValue && productBatch.ManufacturingDate.HasValue)
            {
                request.ManufacturingDate = productBatch.ManufacturingDate;
            }

            if (!isInflow)
            {
                if (!productBatch.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Batch '{normBatch}' for product '{product.ProductCode}' is inactive and cannot be issued or dispatched.");
                }

                if (productBatch.ExpiryDate.HasValue)
                {
                    var txDate = (request.TransactionDate ?? DateTime.UtcNow).Date;
                    if (productBatch.ExpiryDate.Value.Date < txDate)
                    {
                        throw new InvalidOperationException(
                            $"Batch '{normBatch}' for product '{product.ProductCode}' expired on {productBatch.ExpiryDate.Value:yyyy-MM-dd} and cannot be issued or dispatched.");
                    }
                }
            }
        }
        else
        {
            if (isInflow)
            {
                var newBatch = ERP.Domain.Features.Inventory.Batch.ProductBatch.Create(
                    tenantId,
                    product.ProductId,
                    normBatch,
                    request.ManufacturingDate,
                    request.ExpiryDate);

                await _inventoryRepository.AddBatchAsync(newBatch);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Batch '{normBatch}' does not exist for product '{product.ProductCode}'. Stock cannot be issued from an unestablished batch.");
            }
        }
    }

    private async Task ValidateTransferBatchAsync(
        Guid tenantId,
        Product product,
        string normBatch,
        PostTransferMovementRequest request)
    {
        if (product.TrackingMode != TrackingMode.Batch && product.TrackingMode != TrackingMode.BatchAndSerial)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(normBatch))
        {
            throw new InvalidOperationException($"BatchNumber is required for batch-tracked product '{product.ProductCode}'.");
        }

        var batchInTenant = await _inventoryRepository.GetBatchByNumberAsync(tenantId, normBatch);
        if (batchInTenant != null && batchInTenant.ProductId != product.ProductId)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' is already associated with another product in this tenant. A batch number cannot be reused for a different product.");
        }

        var productBatch = await _inventoryRepository.GetBatchAsync(tenantId, product.ProductId, normBatch);
        if (productBatch == null)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' does not exist for product '{product.ProductCode}'. Stock cannot be transferred from an unestablished batch.");
        }

        if (!productBatch.IsActive)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' for product '{product.ProductCode}' is inactive and cannot be transferred.");
        }

        if (request.ExpiryDate.HasValue && productBatch.ExpiryDate.HasValue &&
            request.ExpiryDate.Value.Date != productBatch.ExpiryDate.Value.Date)
        {
            throw new InvalidOperationException(
                $"Conflicting expiry date for existing batch '{normBatch}'. Registered: {productBatch.ExpiryDate.Value:yyyy-MM-dd}, Provided: {request.ExpiryDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
        }

        if (request.ManufacturingDate.HasValue && productBatch.ManufacturingDate.HasValue &&
            request.ManufacturingDate.Value.Date != productBatch.ManufacturingDate.Value.Date)
        {
            throw new InvalidOperationException(
                $"Conflicting manufacturing date for existing batch '{normBatch}'. Registered: {productBatch.ManufacturingDate.Value:yyyy-MM-dd}, Provided: {request.ManufacturingDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
        }

        if (!request.ExpiryDate.HasValue && productBatch.ExpiryDate.HasValue)
        {
            request.ExpiryDate = productBatch.ExpiryDate;
        }
        if (!request.ManufacturingDate.HasValue && productBatch.ManufacturingDate.HasValue)
        {
            request.ManufacturingDate = productBatch.ManufacturingDate;
        }
    }

    private static string GenerateTransactionNumber()
    {
        var now = DateTime.UtcNow;
        string shortGuid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"TXN-{now:yyyyMMdd}-{now:HHmmssfff}-{shortGuid}";
    }
}
