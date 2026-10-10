using System;
using System.Collections.Generic;

namespace Domain.Features.Procurement.PurchaseOrder;

public class CreatePurchaseOrderItemRequest
{
    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitPrice { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid? SupplierId { get; set; }

    public DateTime? OrderDate { get; set; }

    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}
