using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Sales.Shipment;

public class SalesShipment : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SalesOrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? WarehouseId { get; set; }

    public string ShipmentNumber { get; set; } = string.Empty;

    public SalesShipmentStatus Status { get; set; } = SalesShipmentStatus.Draft;

    public FulfillmentMethod Method { get; set; } = FulfillmentMethod.Delivery;

    public DateTime ShipmentDate { get; set; } = DateTime.UtcNow;

    public DateTime? DispatchedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? CarrierName { get; set; }

    public string? TrackingNumber { get; set; }

    public string? RecipientName { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? VehicleNumber { get; set; }

    public string? Notes { get; set; }

    public Guid? InvoiceId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SalesShipmentItem> Items { get; set; } = new();

    public static SalesShipment Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid salesOrderId,
        Guid customerId,
        string shipmentNumber,
        FulfillmentMethod method = FulfillmentMethod.Delivery,
        Guid? warehouseId = null,
        DateTime? shipmentDate = null,
        string? carrierName = null,
        string? trackingNumber = null,
        string? recipientName = null,
        string? deliveryAddress = null,
        string? vehicleNumber = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(shipmentNumber))
            throw new ArgumentException("Shipment number is required.", nameof(shipmentNumber));

        return new SalesShipment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SalesOrderId = salesOrderId,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            ShipmentNumber = shipmentNumber.Trim(),
            Method = method,
            Status = SalesShipmentStatus.Draft,
            ShipmentDate = shipmentDate ?? DateTime.UtcNow,
            CarrierName = carrierName?.Trim(),
            TrackingNumber = trackingNumber?.Trim(),
            RecipientName = recipientName?.Trim(),
            DeliveryAddress = deliveryAddress?.Trim(),
            VehicleNumber = vehicleNumber?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true
        };
    }

    public SalesShipmentItem AddItem(SalesShipmentItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (Status != SalesShipmentStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to shipment '{ShipmentNumber}' because it is in status '{Status}'. Only Draft shipments can be modified.");

        item.SalesShipmentId = this.Id;
        Items.Add(item);
        return item;
    }

    public void MarkDispatched(DateTime? dispatchedAt = null)
    {
        if (Status == SalesShipmentStatus.Dispatched)
            return; // Idempotent check

        if (Status == SalesShipmentStatus.Delivered)
            throw new InvalidOperationException($"Shipment '{ShipmentNumber}' is already delivered.");

        if (Status == SalesShipmentStatus.Cancelled)
            throw new InvalidOperationException($"Cancelled shipment '{ShipmentNumber}' cannot be dispatched.");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException($"Cannot dispatch shipment '{ShipmentNumber}' with no active line items.");

        Status = SalesShipmentStatus.Dispatched;
        DispatchedAt = dispatchedAt ?? DateTime.UtcNow;
    }

    public void MarkDelivered(DateTime? deliveredAt = null, string? recipientName = null)
    {
        if (Status == SalesShipmentStatus.Delivered)
            return; // Idempotent check

        if (Status == SalesShipmentStatus.Cancelled)
            throw new InvalidOperationException($"Cancelled shipment '{ShipmentNumber}' cannot be delivered.");

        if (Status != SalesShipmentStatus.Dispatched && Status != SalesShipmentStatus.Draft)
            throw new InvalidOperationException($"Cannot deliver shipment '{ShipmentNumber}' in status '{Status}'.");

        // If transitioning directly from Draft (e.g. StoreHandover or CustomerPickup)
        if (Status == SalesShipmentStatus.Draft)
        {
            if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
                throw new InvalidOperationException($"Cannot deliver shipment '{ShipmentNumber}' with no active line items.");

            DispatchedAt ??= deliveredAt ?? DateTime.UtcNow;
        }

        Status = SalesShipmentStatus.Delivered;
        DeliveredAt = deliveredAt ?? DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(recipientName))
        {
            RecipientName = recipientName.Trim();
        }
    }

    public void Cancel()
    {
        if (Status == SalesShipmentStatus.Dispatched || Status == SalesShipmentStatus.Delivered)
            throw new InvalidOperationException($"Cannot cancel shipment in status '{Status}'. Dispatched/delivered shipments require a return transaction.");

        if (Status == SalesShipmentStatus.Cancelled)
            return; // Idempotent

        Status = SalesShipmentStatus.Cancelled;
    }
}
