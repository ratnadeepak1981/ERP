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

    public Guid? InvoiceId { get; set; }

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

public class SalesOrderDispatchAvailabilityDto
{
    public Guid SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? DefaultWarehouseId { get; set; }
    public string? DefaultWarehouseName { get; set; }
    public Guid? DefaultLocationId { get; set; }
    public string? DefaultLocationCode { get; set; }
    public string? DefaultLocationName { get; set; }
    public Guid? SelectedWarehouseId { get; set; }
    public string? SelectedWarehouseName { get; set; }
    public Guid? SelectedLocationId { get; set; }
    public string? SelectedLocationCode { get; set; }
    public string? SelectedLocationName { get; set; }
    public bool CanDefaultLocationFulfil { get; set; }
    public bool CanFulfil { get; set; }
    public bool HasShortages { get; set; }
    public List<EligibleLocationDto> EligibleLocations { get; set; } = new();
    public List<SalesOrderDispatchLineAvailabilityDto> Lines { get; set; } = new();
}

public class EligibleLocationDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class SalesOrderDispatchLineAvailabilityDto
{
    public Guid SalesOrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal AlreadyDispatchedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal ShortageQuantity { get; set; }
    public bool CanFulfil { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
}

public class DispatchOrderRequest
{
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string? ShipmentNumber { get; set; }
    public FulfillmentMethod Method { get; set; } = FulfillmentMethod.Delivery;
    public DateTime? ShipmentDate { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? RecipientName { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Notes { get; set; }
}
