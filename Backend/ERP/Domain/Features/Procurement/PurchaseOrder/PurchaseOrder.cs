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

    public decimal TotalAmount { get; set; }

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
            TotalAmount = 0,
            IsActive = true
        };
    }

    public void AddItem(Guid productId, decimal quantity, decimal unitPrice, Guid? unitOfMeasureId = null)
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to purchase order '{OrderNumber}' because it is in status '{Status}'. Only Draft orders can be modified.");

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("UnitPrice cannot be negative.", nameof(unitPrice));

        // Consistent currency and quantity rounding (standard away-from-zero rounding)
        decimal roundedUnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        decimal roundedQuantity = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        decimal lineTotal = Math.Round(roundedQuantity * roundedUnitPrice, 2, MidpointRounding.AwayFromZero);

        var item = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = this.Id,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            Quantity = roundedQuantity,
            UnitPrice = roundedUnitPrice,
            LineTotal = lineTotal,
            IsActive = true
        };

        Items.Add(item);
        RecalculateTotal();
    }

    public void RecalculateTotal()
    {
        decimal sum = 0;
        foreach (var item in Items)
        {
            if (item.IsActive)
            {
                sum += item.LineTotal;
            }
        }
        TotalAmount = Math.Round(sum, 2, MidpointRounding.AwayFromZero);
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
