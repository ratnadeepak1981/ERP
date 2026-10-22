using System;
using ERP.Domain.Common;

namespace Domain.Features.Sales.Invoice;

public class SalesInvoiceItem : Auditable
{
    public Guid Id { get; set; }

    public Guid SalesInvoiceId { get; set; }

    public Guid? SalesOrderItemId { get; set; }

    public Guid? SalesShipmentItemId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Subtotal { get; set; }

    public decimal LineTotal { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = null!;

    public static SalesInvoiceItem Create(
        Guid salesInvoiceId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate = 0,
        decimal taxRate = 0,
        Guid? salesOrderItemId = null,
        Guid? salesShipmentItemId = null,
        Guid? unitOfMeasureId = null,
        string? notes = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        decimal roundedQty = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        decimal roundedPrice = Math.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        decimal subtotal = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);

        decimal discountAmt = 0;
        if (discountRate > 0)
        {
            discountAmt = Math.Round(subtotal * (discountRate / 100m), 4, MidpointRounding.AwayFromZero);
        }

        decimal taxable = subtotal - discountAmt;
        decimal taxAmt = 0;
        if (taxRate > 0)
        {
            taxAmt = Math.Round(taxable * (taxRate / 100m), 4, MidpointRounding.AwayFromZero);
        }

        decimal lineTotal = taxable + taxAmt;

        return new SalesInvoiceItem
        {
            Id = Guid.NewGuid(),
            SalesInvoiceId = salesInvoiceId,
            SalesOrderItemId = salesOrderItemId,
            SalesShipmentItemId = salesShipmentItemId,
            ProductId = productId,
            Quantity = roundedQty,
            UnitPrice = roundedPrice,
            DiscountRate = discountRate,
            DiscountAmount = discountAmt,
            TaxRate = taxRate,
            TaxAmount = taxAmt,
            Subtotal = subtotal,
            LineTotal = lineTotal,
            UnitOfMeasureId = unitOfMeasureId,
            Notes = notes?.Trim(),
            IsActive = true
        };
    }

    public void RecalculateAmounts()
    {
        decimal roundedQty = Math.Round(Quantity, 4, MidpointRounding.AwayFromZero);
        decimal roundedPrice = Math.Round(UnitPrice, 4, MidpointRounding.AwayFromZero);
        Subtotal = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);

        if (DiscountRate > 0)
        {
            DiscountAmount = Math.Round(Subtotal * (DiscountRate / 100m), 4, MidpointRounding.AwayFromZero);
        }
        else
        {
            DiscountAmount = 0;
        }

        decimal taxable = Subtotal - DiscountAmount;
        if (TaxRate > 0)
        {
            TaxAmount = Math.Round(taxable * (TaxRate / 100m), 4, MidpointRounding.AwayFromZero);
        }
        else
        {
            TaxAmount = 0;
        }

        LineTotal = taxable + TaxAmount;
    }
}
