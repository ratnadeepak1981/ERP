using System;

namespace Domain.Features.Sales.SalesOrder;

public class SalesOrderItem
{
    public Guid Id { get; set; }

    public Guid SalesOrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; } = 0;

    public decimal DiscountAmount { get; set; } = 0;

    public decimal TaxRate { get; set; } = 0;

    public decimal TaxAmount { get; set; } = 0;

    public decimal SubTotal { get; set; }

    public decimal LineTotal { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = null!;
}
