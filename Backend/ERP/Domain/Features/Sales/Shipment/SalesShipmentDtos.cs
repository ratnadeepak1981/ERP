using System;
using System.Collections.Generic;

namespace Domain.Features.Sales.Shipment;

public class CreateSalesShipmentRequest
{
    public Guid SalesOrderId { get; set; }

    public string ShipmentNumber { get; set; } = string.Empty;

    public FulfillmentMethod Method { get; set; } = FulfillmentMethod.Delivery;

    public Guid? WarehouseId { get; set; }

    public DateTime? ShipmentDate { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingNumber { get; set; }

    public string? RecipientName { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? VehicleNumber { get; set; }

    public string? Notes { get; set; }

    public List<CreateSalesShipmentItemRequest> Items { get; set; } = new();
}

public class CreateSalesShipmentItemRequest
{
    public Guid SalesOrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid WarehouseId { get; set; }

    public Guid WarehouseLocationId { get; set; }

    public decimal QuantityDispatched { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? BatchNumber { get; set; }

    public string? SerialNumber { get; set; }

    public string? Notes { get; set; }
}

public class CreateShipmentFromOrderRequest
{
    public string ShipmentNumber { get; set; } = string.Empty;

    public FulfillmentMethod Method { get; set; } = FulfillmentMethod.Delivery;

    public Guid? DefaultWarehouseId { get; set; }

    public Guid? DefaultLocationId { get; set; }

    public DateTime? ShipmentDate { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingNumber { get; set; }

    public string? RecipientName { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? VehicleNumber { get; set; }

    public string? Notes { get; set; }

    // Optional override for partial line quantities. If omitted, takes full remaining unfulfilled quantity.
    public List<CreateSalesShipmentItemRequest>? Items { get; set; }
}

public class UpdateSalesShipmentRequest
{
    public FulfillmentMethod? Method { get; set; }

    public DateTime? ShipmentDate { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingNumber { get; set; }

    public string? RecipientName { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? VehicleNumber { get; set; }

    public string? Notes { get; set; }

    public List<CreateSalesShipmentItemRequest>? Items { get; set; }
}

public class DeliverShipmentRequest
{
    public DateTime? DeliveredAt { get; set; }

    public string? RecipientName { get; set; }

    public string? Notes { get; set; }
}

public class SalesShipmentDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SalesOrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid? WarehouseId { get; set; }

    public string ShipmentNumber { get; set; } = string.Empty;

    public SalesShipmentStatus Status { get; set; }

    public FulfillmentMethod Method { get; set; }

    public DateTime ShipmentDate { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingNumber { get; set; }

    public string? RecipientName { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? VehicleNumber { get; set; }

    public string? Notes { get; set; }

    public List<SalesShipmentItemDto> Items { get; set; } = new();
}

public class SalesShipmentItemDto
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
}
