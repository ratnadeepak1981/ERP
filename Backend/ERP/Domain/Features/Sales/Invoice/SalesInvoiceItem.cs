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

    public Guid? TaxCodeId { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxInclusive { get; set; } = false;

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
        string? notes = null,
        Guid? taxCodeId = null,
        bool isTaxInclusive = false)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        decimal roundedQty = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        decimal roundedPrice = Math.Round(unitPrice, 4, MidpointRounding.AwayFromZero);

        decimal subtotal;
        decimal discountAmt;
        decimal taxable;
        decimal taxAmt;
        decimal lineTotal;

        if (isTaxInclusive)
        {
            decimal gross = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);
            discountAmt = discountRate > 0 ? Math.Round(gross * (discountRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            decimal discountedGross = gross - discountAmt;
            taxable = taxRate > 0 ? Math.Round(discountedGross / (1m + (taxRate / 100m)), 4, MidpointRounding.AwayFromZero) : discountedGross;
            taxAmt = discountedGross - taxable;
            subtotal = gross;
            lineTotal = discountedGross;
        }
        else
        {
            subtotal = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);
            discountAmt = discountRate > 0 ? Math.Round(subtotal * (discountRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            taxable = subtotal - discountAmt;
            taxAmt = taxRate > 0 ? Math.Round(taxable * (taxRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            lineTotal = taxable + taxAmt;
        }

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
            TaxCodeId = taxCodeId,
            TaxRate = taxRate,
            TaxAmount = taxAmt,
            Subtotal = subtotal,
            LineTotal = lineTotal,
            IsTaxInclusive = isTaxInclusive,
            UnitOfMeasureId = unitOfMeasureId,
            Notes = notes?.Trim(),
            IsActive = true
        };
    }

    public void RecalculateAmounts()
    {
        decimal roundedQty = Math.Round(Quantity, 4, MidpointRounding.AwayFromZero);
        decimal roundedPrice = Math.Round(UnitPrice, 4, MidpointRounding.AwayFromZero);

        if (IsTaxInclusive)
        {
            decimal gross = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);
            DiscountAmount = DiscountRate > 0 ? Math.Round(gross * (DiscountRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            decimal discountedGross = gross - DiscountAmount;
            decimal taxable = TaxRate > 0 ? Math.Round(discountedGross / (1m + (TaxRate / 100m)), 4, MidpointRounding.AwayFromZero) : discountedGross;
            TaxAmount = discountedGross - taxable;
            Subtotal = gross;
            LineTotal = discountedGross;
        }
        else
        {
            Subtotal = Math.Round(roundedQty * roundedPrice, 4, MidpointRounding.AwayFromZero);
            DiscountAmount = DiscountRate > 0 ? Math.Round(Subtotal * (DiscountRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            decimal taxable = Subtotal - DiscountAmount;
            TaxAmount = TaxRate > 0 ? Math.Round(taxable * (TaxRate / 100m), 4, MidpointRounding.AwayFromZero) : 0;
            LineTotal = taxable + TaxAmount;
        }
    }
}
