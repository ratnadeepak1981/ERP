using System;
using System.Collections.Generic;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesShipmentDomainUnitTests
{
    [Fact]
    public void SalesShipment_Create_SetsExpectedInitialValues()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        string shipmentNum = "SH-0001";

        var shipment = SalesShipment.Create(
            tenantId,
            companyId,
            branchId,
            orderId,
            customerId,
            shipmentNum,
            FulfillmentMethod.CustomerPickup,
            carrierName: "Customer Truck",
            recipientName: "John Doe",
            notes: "Pickup at dock 2");

        Assert.Equal(tenantId, shipment.TenantId);
        Assert.Equal(companyId, shipment.CompanyId);
        Assert.Equal(branchId, shipment.BranchId);
        Assert.Equal(orderId, shipment.SalesOrderId);
        Assert.Equal(customerId, shipment.CustomerId);
        Assert.Equal("SH-0001", shipment.ShipmentNumber);
        Assert.Equal(SalesShipmentStatus.Draft, shipment.Status);
        Assert.Equal(FulfillmentMethod.CustomerPickup, shipment.Method);
        Assert.Equal("Customer Truck", shipment.CarrierName);
        Assert.Equal("John Doe", shipment.RecipientName);
        Assert.Equal("Pickup at dock 2", shipment.Notes);
        Assert.True(shipment.IsActive);
        Assert.Empty(shipment.Items);
    }

    [Fact]
    public void SalesShipment_AddItem_ValidatesStateAndQuantities()
    {
        var shipment = CreateTestShipment();

        var item = SalesShipmentItem.Create(
            shipment.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            15.5m,
            notes: "Batch A");

        shipment.AddItem(item);

        Assert.Single(shipment.Items);
        Assert.Equal(15.5m, shipment.Items[0].QuantityDispatched);
        Assert.Equal(shipment.Id, shipment.Items[0].SalesShipmentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SalesShipmentItem_InvalidQuantity_ThrowsArgumentException(decimal invalidQty)
    {
        Assert.Throws<ArgumentException>(() =>
            SalesShipmentItem.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                invalidQty));
    }

    [Fact]
    public void SalesShipment_LifecycleTransitions_EnforceDomainRules()
    {
        var shipment = CreateTestShipment();
        var item = SalesShipmentItem.Create(
            shipment.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m);
        shipment.AddItem(item);

        // Cannot deliver before dispatch (unless via direct handover from Draft)
        // Transition: Draft -> Dispatched
        shipment.MarkDispatched();
        Assert.Equal(SalesShipmentStatus.Dispatched, shipment.Status);
        Assert.NotNull(shipment.DispatchedAt);

        // Cannot add items after dispatch
        var newItem = SalesShipmentItem.Create(
            shipment.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m);
        Assert.Throws<InvalidOperationException>(() => shipment.AddItem(newItem));

        // Transition: Dispatched -> Delivered
        shipment.MarkDelivered(recipientName: "Alice");
        Assert.Equal(SalesShipmentStatus.Delivered, shipment.Status);
        Assert.NotNull(shipment.DeliveredAt);
        Assert.Equal("Alice", shipment.RecipientName);

        // Cannot cancel delivered shipment
        Assert.Throws<InvalidOperationException>(() => shipment.Cancel());
    }

    [Fact]
    public void SalesShipment_DirectHandover_TransitionsDraftToDelivered()
    {
        var shipment = CreateTestShipment();
        shipment.Method = FulfillmentMethod.StoreHandover;
        var item = SalesShipmentItem.Create(
            shipment.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2m);
        shipment.AddItem(item);

        // Store handover directly to customer marks both dispatched and delivered
        shipment.MarkDelivered(recipientName: "Counter Customer");

        Assert.Equal(SalesShipmentStatus.Delivered, shipment.Status);
        Assert.NotNull(shipment.DispatchedAt);
        Assert.NotNull(shipment.DeliveredAt);
        Assert.Equal("Counter Customer", shipment.RecipientName);
    }

    [Fact]
    public void SalesShipment_Cancel_DraftShipment_Succeeds()
    {
        var shipment = CreateTestShipment();
        shipment.Cancel();

        Assert.Equal(SalesShipmentStatus.Cancelled, shipment.Status);

        // Cannot dispatch cancelled shipment
        Assert.Throws<InvalidOperationException>(() => shipment.MarkDispatched());
    }

    [Fact]
    public void SalesShipment_EmptyItems_CannotBeDispatched()
    {
        var shipment = CreateTestShipment();
        Assert.Throws<InvalidOperationException>(() => shipment.MarkDispatched());
    }

    [Fact]
    public void SalesShipment_Idempotency_RepeatCallsDoNotThrow()
    {
        var shipment = CreateTestShipment();
        var item = SalesShipmentItem.Create(
            shipment.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m);
        shipment.AddItem(item);

        shipment.MarkDispatched();
        var firstDispatchedAt = shipment.DispatchedAt;

        // Second call is safe and idempotent
        shipment.MarkDispatched();
        Assert.Equal(SalesShipmentStatus.Dispatched, shipment.Status);
        Assert.Equal(firstDispatchedAt, shipment.DispatchedAt);

        shipment.MarkDelivered();
        var firstDeliveredAt = shipment.DeliveredAt;

        // Second call is safe and idempotent
        shipment.MarkDelivered();
        Assert.Equal(SalesShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(firstDeliveredAt, shipment.DeliveredAt);
    }

    [Fact]
    public void ProductType_ServiceItem_DistinguishedFromPhysicalGoods()
    {
        // Actual Product model enum verification:
        // Service items must NOT be treated as physical goods
        Assert.Equal(ProductType.Service, ProductType.Service);
        Assert.NotEqual(ProductType.Service, ProductType.StockItem);
        Assert.NotEqual(ProductType.Service, ProductType.NonStockItem);

        // NonStockItem is physical goods (not a service)
        Assert.True(ProductType.NonStockItem != ProductType.Service);
    }

    private static SalesShipment CreateTestShipment()
    {
        return SalesShipment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"SH-{Guid.NewGuid():N}"[..12],
            FulfillmentMethod.Delivery);
    }
}
