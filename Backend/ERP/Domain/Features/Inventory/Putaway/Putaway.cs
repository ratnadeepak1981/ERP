using System;
using System.Collections.Generic;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.Inventory.Putaway;

public class Putaway : Auditable, ITenantScopedEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public Guid PutawayId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }

    public string PutawayNumber { get; set; } = string.Empty;
    public DateTime PutawayDate { get; set; } = DateTime.UtcNow;

    public PutawaySourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public PutawayStatus Status { get; set; } = PutawayStatus.Draft;
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<PutawayLine> Lines { get; set; } = new();

    public static Putaway Create(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string putawayNumber,
        PutawaySourceDocumentType sourceDocType,
        Guid sourceDocId,
        string sourceDocNumber,
        Guid warehouseId,
        DateTime? putawayDate = null,
        string? notes = null)
    {
        return new Putaway
        {
            PutawayId = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            PutawayNumber = putawayNumber.Trim(),
            SourceDocumentType = sourceDocType,
            SourceDocumentId = sourceDocId,
            SourceDocumentNumber = sourceDocNumber.Trim(),
            WarehouseId = warehouseId,
            PutawayDate = putawayDate ?? DateTime.UtcNow,
            Status = PutawayStatus.Draft,
            Notes = notes?.Trim()
        };
    }

    public void AddLine(PutawayLine line)
    {
        if (Status != PutawayStatus.Draft)
            throw new InvalidOperationException($"Cannot add lines to Putaway '{PutawayNumber}' because it is in status '{Status}'. Only Draft Putaways can be modified.");

        line.PutawayId = this.PutawayId;
        Lines.Add(line);
    }

    public void Complete()
    {
        if (Status != PutawayStatus.Draft)
            throw new InvalidOperationException($"Putaway '{PutawayNumber}' cannot be confirmed because it is in status '{Status}'. Only Draft Putaways can be confirmed.");

        Status = PutawayStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != PutawayStatus.Draft)
            throw new InvalidOperationException($"Putaway '{PutawayNumber}' cannot be cancelled because it is in status '{Status}'. Only Draft Putaways can be cancelled.");

        Status = PutawayStatus.Cancelled;
    }
}
