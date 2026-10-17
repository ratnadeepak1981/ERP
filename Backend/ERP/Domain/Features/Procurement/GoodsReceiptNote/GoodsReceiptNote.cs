using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public class GoodsReceiptNote : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid GoodsReceiptNoteId { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid? SupplierId { get; set; }

    public string GrnNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    public GoodsReceiptNoteStatus Status { get; set; } = GoodsReceiptNoteStatus.Draft;

    public string? DeliveryNoteNumber { get; set; }

    public string? Remarks { get; set; }

    public decimal TotalAmount { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<GoodsReceiptNoteLine> Lines { get; set; } = new();

    public static GoodsReceiptNote Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid purchaseOrderId,
        string grnNumber,
        DateTime? receiptDate = null,
        Guid? supplierId = null,
        string? deliveryNoteNumber = null,
        string? remarks = null)
    {
        return new GoodsReceiptNote
        {
            GoodsReceiptNoteId = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PurchaseOrderId = purchaseOrderId,
            GrnNumber = grnNumber.Trim(),
            ReceiptDate = receiptDate ?? DateTime.UtcNow,
            Status = GoodsReceiptNoteStatus.Draft,
            SupplierId = supplierId,
            DeliveryNoteNumber = deliveryNoteNumber?.Trim(),
            Remarks = remarks?.Trim(),
            TotalAmount = 0
        };
    }

    public void AddLine(GoodsReceiptNoteLine line)
    {
        if (Status != GoodsReceiptNoteStatus.Draft)
            throw new InvalidOperationException($"Cannot add lines to GRN '{GrnNumber}' because it is in status '{Status}'. Only Draft GRNs can be modified.");

        line.GoodsReceiptNoteId = this.GoodsReceiptNoteId;
        Lines.Add(line);
        RecalculateTotal();
    }

    public void RecalculateTotal()
    {
        decimal sum = 0;
        foreach (var line in Lines)
        {
            sum += line.LineTotal;
        }
        TotalAmount = Math.Round(sum, 2, MidpointRounding.AwayFromZero);
    }

    public void MarkConfirmed()
    {
        if (Status == GoodsReceiptNoteStatus.Confirmed)
            throw new InvalidOperationException($"GRN '{GrnNumber}' is already confirmed.");

        if (Status == GoodsReceiptNoteStatus.Cancelled)
            throw new InvalidOperationException($"Cancelled GRN '{GrnNumber}' cannot be confirmed.");

        if (Lines.Count == 0)
            throw new InvalidOperationException($"Cannot confirm GRN '{GrnNumber}' because it contains no lines.");

        Status = GoodsReceiptNoteStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == GoodsReceiptNoteStatus.Confirmed)
            throw new InvalidOperationException("Confirmed GRNs cannot be cancelled.");

        if (Status == GoodsReceiptNoteStatus.Cancelled)
            throw new InvalidOperationException("GRN is already cancelled.");

        Status = GoodsReceiptNoteStatus.Cancelled;
    }
}
