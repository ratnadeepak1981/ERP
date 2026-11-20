using System;

namespace Domain.Features.Sales.SalesQuotation;

public class SalesQuotationItem
{
    public Guid Id { get; set; }

    public Guid SalesQuotationId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountRate { get; set; } = 0;

    public decimal DiscountAmount { get; set; } = 0;

    public Guid? TaxCodeId { get; set; }

    public decimal TaxRate { get; set; } = 0;

    public decimal TaxAmount { get; set; } = 0;

    public decimal SubTotal { get; set; }

    public decimal LineTotal { get; set; }

    public bool IsTaxInclusive { get; set; } = false;

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = null!;

    public void RecalculateLine()
    {
        if (Quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(Quantity), "Quantity cannot be negative.");

        if (UnitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(UnitPrice), "UnitPrice cannot be negative.");

        if (DiscountRate < 0 || DiscountRate > 100)
            throw new ArgumentOutOfRangeException(nameof(DiscountRate), "DiscountRate must be between 0 and 100.");

        decimal rawSubTotal = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
        DiscountAmount = Math.Round(rawSubTotal * (DiscountRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal taxableAmount = Math.Max(0m, rawSubTotal - DiscountAmount);

        if (TaxRate > 0)
        {
            if (IsTaxInclusive)
            {
                // Tax inclusive: Tax is extracted from the discounted taxable amount
                TaxAmount = Math.Round(taxableAmount - (taxableAmount / (1m + (TaxRate / 100m))), 2, MidpointRounding.AwayFromZero);
                SubTotal = taxableAmount - TaxAmount;
                LineTotal = taxableAmount;
            }
            else
            {
                // Tax exclusive: Tax is added on top of taxable amount
                TaxAmount = Math.Round(taxableAmount * (TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
                SubTotal = taxableAmount;
                LineTotal = SubTotal + TaxAmount;
            }
        }
        else
        {
            TaxAmount = 0m;
            SubTotal = taxableAmount;
            LineTotal = SubTotal;
        }
    }
}
