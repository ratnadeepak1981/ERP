using System;
using System.Collections.Generic;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.PurchaseRequisition;

public enum PurchaseRequisitionStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}

public class PurchaseRequisition : ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public string RequisitionNumber { get; set; } = string.Empty;

    public DateTime RequisitionDate { get; set; } = DateTime.UtcNow;

    public DateTime? RequiredDate { get; set; }

    public Guid RequestorUserId { get; set; }

    public PurchaseRequisitionStatus Status { get; set; } = PurchaseRequisitionStatus.Draft;

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public List<PurchaseRequisitionItem> Items { get; set; } = new();

    public static PurchaseRequisition Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string requisitionNumber,
        Guid requestorUserId,
        DateTime? requisitionDate = null,
        DateTime? requiredDate = null,
        string? notes = null)
    {
        return new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            RequisitionNumber = requisitionNumber.Trim(),
            RequestorUserId = requestorUserId,
            RequisitionDate = requisitionDate ?? DateTime.UtcNow,
            RequiredDate = requiredDate,
            Notes = notes?.Trim(),
            Status = PurchaseRequisitionStatus.Draft,
            TotalAmount = 0,
            IsActive = true
        };
    }

    public void AddItem(Guid productId, decimal quantity, decimal? estimatedUnitPrice = null, Guid? unitOfMeasureId = null, string? remarks = null)
    {
        if (Status != PurchaseRequisitionStatus.Draft)
            throw new InvalidOperationException($"Cannot add items to purchase requisition '{RequisitionNumber}' because it is in status '{Status}'. Only Draft requisitions can be modified.");

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        decimal unitPrice = estimatedUnitPrice ?? 0;
        if (unitPrice < 0)
            throw new ArgumentException("Estimated unit price cannot be negative.", nameof(estimatedUnitPrice));

        decimal roundedUnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        decimal roundedQuantity = Math.Round(quantity, 4, MidpointRounding.AwayFromZero);
        decimal lineTotal = Math.Round(roundedQuantity * roundedUnitPrice, 2, MidpointRounding.AwayFromZero);

        var item = new PurchaseRequisitionItem
        {
            Id = Guid.NewGuid(),
            PurchaseRequisitionId = this.Id,
            ProductId = productId,
            UnitOfMeasureId = unitOfMeasureId,
            Quantity = roundedQuantity,
            EstimatedUnitPrice = roundedUnitPrice,
            EstimatedLineTotal = lineTotal,
            Remarks = remarks?.Trim(),
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
                sum += item.EstimatedLineTotal;
            }
        }
        TotalAmount = Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    public void Submit()
    {
        if (Status != PurchaseRequisitionStatus.Draft)
            throw new InvalidOperationException($"Only Draft requisitions can be submitted. Current status: {Status}");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot submit a purchase requisition with no active line items.");

        Status = PurchaseRequisitionStatus.Submitted;
    }

    public void Approve()
    {
        if (Status != PurchaseRequisitionStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted requisitions can be approved. Current status: {Status}");

        Status = PurchaseRequisitionStatus.Approved;
    }

    public void AutoApprove()
    {
        if (Status != PurchaseRequisitionStatus.Draft && Status != PurchaseRequisitionStatus.Submitted)
            throw new InvalidOperationException($"Requisitions in status '{Status}' cannot be automatically approved.");

        if (Items.Count == 0 || !Items.Exists(i => i.IsActive))
            throw new InvalidOperationException("Cannot approve a purchase requisition with no active line items.");

        Status = PurchaseRequisitionStatus.Approved;
    }

    public void Reject()
    {
        if (Status != PurchaseRequisitionStatus.Submitted)
            throw new InvalidOperationException($"Only Submitted requisitions can be rejected. Current status: {Status}");

        Status = PurchaseRequisitionStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status == PurchaseRequisitionStatus.Cancelled)
            throw new InvalidOperationException("Purchase requisition is already cancelled.");

        Status = PurchaseRequisitionStatus.Cancelled;
    }
}
