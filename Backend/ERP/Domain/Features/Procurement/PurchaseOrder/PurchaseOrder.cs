using System;
using System.Collections.Generic;

using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.PurchaseOrder;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Fulfilled = 4,
    Cancelled = 5,
    Rejected = 6
}

public class PurchaseOrder : ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public List<PurchaseOrderItem> Items { get; set; } = new();

    public static PurchaseOrder Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string orderNumber,
        DateTime? orderDate = null,
        Guid? supplierId = null,
        Guid? createdByUserId = null)
    {
        return new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            SupplierId = supplierId,
            CreatedByUserId = createdByUserId,
            OrderNumber = orderNumber.Trim(),
            OrderDate = orderDate ?? DateTime.UtcNow,
            Status = PurchaseOrderStatus.Draft,
            SubTotal = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            IsActive = true
        };
    }

    public PurchaseOrderItem AddItem(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        Guid? unitOfMeasureId = null,
        decimal discountRate = 0m,
        Guid? taxCodeId = null,
        decimal taxRate = 0m,
        bool isTaxInclusive = false,
        string? notes = null)
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to purchase order '{OrderNumber}' because it is in status '{Status}'. Only Draft orders can be modified.");

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));
        if (discountRate < 0 || discountRate > 100)
            throw new ArgumentException("Discount rate must be between 0 and 100.", nameof(discountRate));
        if (taxRate < 0)
            throw new ArgumentException("Tax rate cannot be negative.", nameof(taxRate));

        decimal roundedQuantity = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        decimal roundedUnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        decimal roundedDiscountRate = Math.Round(discountRate, 2, MidpointRounding.AwayFromZero);
        decimal roundedTaxRate = Math.Round(taxRate, 4, MidpointRounding.AwayFromZero);

        decimal lineSubTotal;
        decimal lineDiscount;
        decimal taxableAmount;
        decimal lineTax;
        decimal lineTotal;

        if (isTaxInclusive)
        {
            decimal gross = Math.Round(roundedQuantity * roundedUnitPrice, 2, MidpointRounding.AwayFromZero);
            lineDiscount = Math.Round(gross * (roundedDiscountRate / 100m), 2, MidpointRounding.AwayFromZero);
            decimal discountedGross = gross - lineDiscount;
            taxableAmount = Math.Round(discountedGross / (1m + (roundedTaxRate / 100m)), 2, MidpointRounding.AwayFromZero);
            lineTax = discountedGross - taxableAmount;
            lineSubTotal = gross;
            lineTotal = discountedGross;
        }
        else
        {
            lineSubTotal = Math.Round(roundedQuantity * roundedUnitPrice, 2, MidpointRounding.AwayFromZero);
            lineDiscount = Math.Round(lineSubTotal * (roundedDiscountRate / 100m), 2, MidpointRounding.AwayFromZero);
            taxableAmount = lineSubTotal - lineDiscount;
            lineTax = Math.Round(taxableAmount * (roundedTaxRate / 100m), 2, MidpointRounding.AwayFromZero);
            lineTotal = Math.Round(taxableAmount + lineTax, 2, MidpointRounding.AwayFromZero);
        }

        var item = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = this.Id,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            Quantity = roundedQuantity,
            UnitPrice = roundedUnitPrice,
            DiscountRate = roundedDiscountRate,
            DiscountAmount = lineDiscount,
            TaxCodeId = taxCodeId,
            TaxRate = roundedTaxRate,
            TaxAmount = lineTax,
            SubTotal = lineSubTotal,
            LineTotal = lineTotal,
            IsTaxInclusive = isTaxInclusive,
            Notes = notes?.Trim(),
            IsActive = true
        };

        Items.Add(item);
        RecalculateTotal();
        return item;
    }

    public void RecalculateTotal()
    {
        decimal subTotal = 0;
        decimal discountAmount = 0;
        decimal taxAmount = 0;
        decimal totalAmount = 0;

        foreach (var item in Items)
        {
            if (item.IsActive)
            {
                subTotal += item.SubTotal;
                discountAmount += item.DiscountAmount;
                taxAmount += item.TaxAmount;
                totalAmount += item.LineTotal;
            }
        }

        SubTotal = Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
        DiscountAmount = Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
        TaxAmount = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
        TotalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Only Draft orders can be submitted. Current status: {Status}");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot submit a purchase order with no active line items.");

        Status = PurchaseOrderStatus.Submitted;
    }

    public void Approve()
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted orders can be approved. Current status: {Status}");

        Status = PurchaseOrderStatus.Approved;
    }

    public void AutoApprove()
    {
        if (Status != PurchaseOrderStatus.Draft && Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException($"Orders in status '{Status}' cannot be automatically approved.");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot approve a purchase order with no active line items.");

        Status = PurchaseOrderStatus.Approved;
    }

    public void Reject()
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted orders can be rejected. Current status: {Status}");

        Status = PurchaseOrderStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status == PurchaseOrderStatus.Fulfilled)
            throw new InvalidOperationException("Fulfilled orders cannot be cancelled.");

        if (Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException("Purchase order is already cancelled.");

        Status = PurchaseOrderStatus.Cancelled;
    }
}
