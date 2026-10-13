using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Sales.SalesOrder;

public class SalesOrder : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public SalesOrderType DocumentType { get; set; } = SalesOrderType.Standard;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public DateTime? DeliveryDate { get; set; }

    public string Currency { get; set; } = "USD";

    public string? PaymentTerms { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public Guid? OpportunityId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SalesOrderItem> Items { get; set; } = new();

    public static SalesOrder Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        string orderNumber,
        SalesOrderType documentType = SalesOrderType.Standard,
        DateTime? orderDate = null,
        DateTime? deliveryDate = null,
        string? currency = null,
        string? paymentTerms = null,
        string? notes = null,
        Guid? createdByUserId = null,
        Guid? opportunityId = null)
    {
        return new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = customerId,
            OrderNumber = orderNumber.Trim(),
            DocumentType = documentType,
            OrderDate = orderDate ?? DateTime.UtcNow,
            DeliveryDate = deliveryDate,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            PaymentTerms = paymentTerms?.Trim(),
            Notes = notes?.Trim(),
            CreatedByUserId = createdByUserId,
            OpportunityId = opportunityId,
            Status = SalesOrderStatus.Draft,
            SubTotal = 0,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            IsActive = true
        };
    }

    public SalesOrderItem AddItem(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountRate = 0m,
        decimal taxRate = 0m,
        Guid? unitOfMeasureId = null,
        string? notes = null)
    {
        if (Status != SalesOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to sales order '{OrderNumber}' because it is in status '{Status}'. Only Draft orders can be modified.");

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
        decimal roundedTaxRate = Math.Round(taxRate, 2, MidpointRounding.AwayFromZero);

        decimal lineSubTotal = Math.Round(roundedQuantity * roundedUnitPrice, 2, MidpointRounding.AwayFromZero);
        decimal lineDiscount = Math.Round(lineSubTotal * (roundedDiscountRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal taxableAmount = lineSubTotal - lineDiscount;
        decimal lineTax = Math.Round(taxableAmount * (roundedTaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal lineTotal = Math.Round(taxableAmount + lineTax, 2, MidpointRounding.AwayFromZero);

        var item = new SalesOrderItem
        {
            Id = Guid.NewGuid(),
            SalesOrderId = this.Id,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            Quantity = roundedQuantity,
            UnitPrice = roundedUnitPrice,
            DiscountRate = roundedDiscountRate,
            DiscountAmount = lineDiscount,
            TaxRate = roundedTaxRate,
            TaxAmount = lineTax,
            SubTotal = lineSubTotal,
            LineTotal = lineTotal,
            Notes = notes?.Trim(),
            IsActive = true
        };

        Items.Add(item);
        RecalculateTotals();
        return item;
    }

    public void RecalculateTotals()
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
        if (Status != SalesOrderStatus.Draft)
            throw new InvalidOperationException($"Only Draft orders can be submitted. Current status: {Status}");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot submit a sales order with no active line items.");

        Status = SalesOrderStatus.Submitted;
    }

    public void Confirm()
    {
        if (Status != SalesOrderStatus.Submitted && Status != SalesOrderStatus.Draft)
            throw new InvalidOperationException($"Orders in status '{Status}' cannot be confirmed. Order must be Draft or Submitted.");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot confirm a sales order with no active line items.");

        Status = SalesOrderStatus.Confirmed;
    }

    public void Reject()
    {
        if (Status != SalesOrderStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted orders can be rejected. Current status: {Status}");

        Status = SalesOrderStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status == SalesOrderStatus.Fulfilled)
            throw new InvalidOperationException("Fulfilled orders cannot be cancelled.");

        if (Status == SalesOrderStatus.Cancelled)
            throw new InvalidOperationException("Sales order is already cancelled.");

        Status = SalesOrderStatus.Cancelled;
    }
}
