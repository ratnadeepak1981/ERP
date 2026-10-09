using System;

namespace Domain.Features.Procurement.PurchaseOrder;

public class PurchaseOrderItem
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsActive { get; set; } = true;
}
