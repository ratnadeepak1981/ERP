using System;
using ERP.Domain.Common;

namespace Domain.Features.Sales.Shipment;

public class SalesShipmentItem : Auditable
{
    public Guid Id { get; set; }

    public Guid SalesShipmentId { get; set; }

    public Guid SalesOrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid WarehouseLocationId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal QuantityDispatched { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = null!;

    public static SalesShipmentItem Create(
        Guid salesShipmentId,
        Guid salesOrderItemId,
        Guid productId,
        Guid warehouseId,
        Guid warehouseLocationId,
        decimal quantityDispatched,
        Guid? unitOfMeasureId = null,
        string? batchNumber = null,
        string? serialNumber = null,
        string? notes = null)
    {
        if (quantityDispatched <= 0)
            throw new ArgumentException("Quantity dispatched must be greater than zero.", nameof(quantityDispatched));

        decimal roundedQuantity = Math.Round(quantityDispatched, 4, MidpointRounding.AwayFromZero);

        return new SalesShipmentItem
        {
            Id = Guid.NewGuid(),
            SalesShipmentId = salesShipmentId,
            SalesOrderItemId = salesOrderItemId,
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            QuantityDispatched = roundedQuantity,
            UnitOfMeasureId = unitOfMeasureId,
            BatchNumber = batchNumber?.Trim(),
            SerialNumber = serialNumber?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true
        };
    }
}
