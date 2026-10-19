using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.Inventory;

public class InventoryTransaction : Auditable, ITenantScopedEntity
{
    public Guid InventoryTransactionId { get; private set; }

    public Guid TenantId { get; private set; }

    public string TransactionNumber { get; private set; } = string.Empty;

    // Movement Classification
    public InventoryMovementType MovementType { get; private set; }

    // Direct Source Document Traceability
    public SourceDocumentType SourceDocumentType { get; private set; }

    public Guid SourceDocumentId { get; private set; }

    // Normalized to Guid.Empty if header-level/unspecified to support SQL Server unique constraints cleanly
    public Guid SourceDocumentLineId { get; private set; }

    public int MovementSequence { get; private set; }

    // Stock Identity Dimensions
    public Guid ProductId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid WarehouseLocationId { get; private set; }

    public Guid? UnitOfMeasureId { get; private set; }

    // Signed Quantities & Values (+ for Inflow, - for Outflow)
    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal TotalCost { get; private set; }

    // Batch & Serial Dimensions
    public string? BatchNumber { get; private set; }

    public string? SerialNumber { get; private set; }

    public DateTime? ExpiryDate { get; private set; }

    // Operational Context
    public DateTime TransactionDate { get; private set; }

    // Reversal Tracking (Self-referencing audit link)
    public Guid? ReversalOfTransactionId { get; private set; }

    public static InventoryTransaction Create(
        Guid tenantId,
        string transactionNumber,
        InventoryMovementType movementType,
        SourceDocumentType sourceDocumentType,
        Guid sourceDocumentId,
        Guid sourceDocumentLineId,
        int movementSequence,
        Guid productId,
        Guid warehouseId,
        Guid warehouseLocationId,
        decimal quantity,
        decimal unitCost = 0,
        Guid? unitOfMeasureId = null,
        string? batchNumber = null,
        string? serialNumber = null,
        DateTime? expiryDate = null,
        DateTime? transactionDate = null,
        Guid? reversalOfTransactionId = null,
        Guid? inventoryTransactionId = null)
    {
        return new InventoryTransaction
        {
            InventoryTransactionId = inventoryTransactionId ?? Guid.NewGuid(),
            TenantId = tenantId,
            TransactionNumber = transactionNumber.Trim(),
            MovementType = movementType,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentLineId = sourceDocumentLineId,
            MovementSequence = movementSequence,
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = Math.Round(Math.Abs(quantity) * unitCost, 2, MidpointRounding.AwayFromZero),
            UnitOfMeasureId = unitOfMeasureId,
            BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            ExpiryDate = expiryDate,
            TransactionDate = transactionDate ?? DateTime.UtcNow,
            ReversalOfTransactionId = reversalOfTransactionId
        };
    }

    private InventoryTransaction()
    {
    }
}
