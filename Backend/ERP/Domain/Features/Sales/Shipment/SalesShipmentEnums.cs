namespace Domain.Features.Sales.Shipment;

public enum SalesShipmentStatus
{
    Draft = 1,
    Dispatched = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum FulfillmentMethod
{
    Delivery = 1,
    CustomerPickup = 2,
    StoreHandover = 3
}
