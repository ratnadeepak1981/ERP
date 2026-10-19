using System;
using ERP.Domain.Common;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public class GoodsReceiptNoteLine : Auditable
{
    public Guid GoodsReceiptNoteLineId { get; set; }

    public Guid GoodsReceiptNoteId { get; set; }

    public Guid PurchaseOrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid WarehouseLocationId { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal UnitCost { get; set; }

    public decimal LineTotal { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsQuarantine { get; set; }

    public string? Remarks { get; set; }

    public static GoodsReceiptNoteLine Create(
        Guid grnId,
        Guid purchaseOrderItemId,
        Guid productId,
        Guid warehouseId,
        Guid warehouseLocationId,
        decimal quantityReceived,
        decimal unitCost,
        Guid? unitOfMeasureId = null,
        string? batchNumber = null,
        string? serialNumber = null,
        DateTime? expiryDate = null,
        bool isQuarantine = false,
        string? remarks = null)
    {
        if (quantityReceived <= 0)
            throw new ArgumentException("Quantity received must be greater than zero.", nameof(quantityReceived));

        if (unitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        decimal roundedQty = Math.Round(quantityReceived, 4, MidpointRounding.AwayFromZero);
        decimal roundedCost = Math.Round(unitCost, 2, MidpointRounding.AwayFromZero);
        decimal lineTotal = Math.Round(roundedQty * roundedCost, 2, MidpointRounding.AwayFromZero);

        return new GoodsReceiptNoteLine
        {
            GoodsReceiptNoteLineId = Guid.NewGuid(),
            GoodsReceiptNoteId = grnId,
            PurchaseOrderItemId = purchaseOrderItemId,
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            QuantityReceived = roundedQty,
            UnitCost = roundedCost,
            LineTotal = lineTotal,
            UnitOfMeasureId = unitOfMeasureId,
            BatchNumber = batchNumber?.Trim(),
            SerialNumber = serialNumber?.Trim(),
            ExpiryDate = expiryDate,
            IsQuarantine = isQuarantine,
            Remarks = remarks?.Trim()
        };
    }
}
