namespace Domain.Features.Sales.SalesOrder;

public enum SalesOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Confirmed = 3,
    Fulfilled = 4,
    Cancelled = 5,
    Rejected = 6
}

public enum SalesOrderType
{
    Standard = 1,
    Direct = 2,
    Service = 3
}
