using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory;

public class InventoryPostingService : IInventoryPostingService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly DomainDbContext _context;

    public InventoryPostingService(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        DomainDbContext context)
    {
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _context = context;
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
        var existingTx = _context.Database.CurrentTransaction;
        var dbTransaction = existingTx == null ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            string normBatch = request.BatchNumber?.Trim() ?? string.Empty;
            string normSerial = request.SerialNumber?.Trim() ?? string.Empty;

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
        // Inflow: Positive quantity
        // Outflow: Negative quantity
        decimal signedQuantity = GetSignedQuantity(request.MovementType, request.Quantity);

        if (signedQuantity < 0)
        {
            // Outflow: Ensure sufficient available stock
            decimal requestedOutflow = Math.Abs(signedQuantity);
            if (requestedOutflow > balance.QuantityAvailable)
            {
                throw new InvalidOperationException(
                    $"Insufficient available inventory at location '{location.LocationCode}'. Requested: {requestedOutflow}, Available: {balance.QuantityAvailable} (On-Hand: {balance.QuantityOnHand}, Quarantined: {balance.QuantityQuarantine}, Allocated: {balance.QuantityAllocated}).");
            }
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
            request.UnitCost,
            request.UnitOfMeasureId ?? product.UnitOfMeasureId,
            normBatch,
            normSerial,
            request.ExpiryDate,
            request.TransactionDate ?? DateTime.UtcNow);

        await _inventoryRepository.AddTransactionAsync(txn);
        await _inventoryRepository.SaveChangesAsync();

        if (dbTransaction != null)
        {
            await dbTransaction.CommitAsync();
        }

        return txn;
    }
    finally
    {
        if (dbTransaction != null)
        {
            await dbTransaction.DisposeAsync();
        }
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

        var existingTx = _context.Database.CurrentTransaction;
        var dbTransaction = existingTx == null ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            string normBatch = request.BatchNumber?.Trim() ?? string.Empty;
            string normSerial = request.SerialNumber?.Trim() ?? string.Empty;

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
            // Moving stock out of quarantine: Must have enough quarantine stock
            if (request.Quantity > srcBalance.QuantityQuarantine)
            {
                throw new InvalidOperationException($"Insufficient quarantine stock at source location. Requested: {request.Quantity}, Quarantine available: {srcBalance.QuantityQuarantine}.");
            }

            // Decrement both on-hand and quarantine at source
            srcBalance.ApplyOnHandChange(-request.Quantity);
            srcBalance.ApplyQuarantineChange(-request.Quantity);
        }
        else
        {
            // Standard transfer: Must have enough available stock
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
            request.UnitCost,
            request.UnitOfMeasureId ?? product.UnitOfMeasureId,
            normBatch,
            normSerial,
            request.ExpiryDate,
            request.TransactionDate ?? DateTime.UtcNow);

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
            request.UnitCost,
            request.UnitOfMeasureId ?? product.UnitOfMeasureId,
            normBatch,
            normSerial,
            request.ExpiryDate,
            request.TransactionDate ?? DateTime.UtcNow);

        await _inventoryRepository.AddTransactionAsync(outTxn);
        await _inventoryRepository.AddTransactionAsync(inTxn);
        await _inventoryRepository.SaveChangesAsync();

        if (dbTransaction != null)
        {
            await dbTransaction.CommitAsync();
        }

        return (outTxn, inTxn);
    }
    finally
    {
        if (dbTransaction != null)
        {
            await dbTransaction.DisposeAsync();
        }
    }
}

    public async Task<InventoryTransaction> PostReversalAsync(Guid tenantId, PostReversalRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var existingTx = _context.Database.CurrentTransaction;
        var dbTransaction = existingTx == null ? await _context.Database.BeginTransactionAsync() : null;
        try
        {
            var originalTxn = await _inventoryRepository.GetTransactionByIdAsync(tenantId, request.OriginalTransactionId)
                ?? throw new KeyNotFoundException("Original inventory transaction not found.");

        if (originalTxn.MovementType == InventoryMovementType.Reversal)
        {
            throw new InvalidOperationException("Cannot reverse a reversal transaction.");
        }

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

        // The reversal direction is opposite to the original movement
        // If original was +Qty (e.g., Receipt), reversal is -QtyToReverse.
        // If original was -Qty (e.g., Issue), reversal is +QtyToReverse.
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
            originalTxn.UnitCost,
            originalTxn.UnitOfMeasureId,
            normBatch,
            normSerial,
            originalTxn.ExpiryDate,
            request.TransactionDate ?? DateTime.UtcNow,
            reversalOfTransactionId: originalTxn.InventoryTransactionId);

        await _inventoryRepository.AddTransactionAsync(reversalTxn);
        await _inventoryRepository.SaveChangesAsync();

        if (dbTransaction != null)
        {
            await dbTransaction.CommitAsync();
        }

        return reversalTxn;
    }
    finally
    {
        if (dbTransaction != null)
        {
            await dbTransaction.DisposeAsync();
        }
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

            InventoryMovementType.Reversal => quantity, // Provided explicitly by reversal handler
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

    private static string GenerateTransactionNumber()
    {
        // Safe concurrency format: TXN-YYYYMMDD-HHMMSSfff-ShortGuid
        var now = DateTime.UtcNow;
        string shortGuid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"TXN-{now:yyyyMMdd}-{now:HHmmssfff}-{shortGuid}";
    }
}
