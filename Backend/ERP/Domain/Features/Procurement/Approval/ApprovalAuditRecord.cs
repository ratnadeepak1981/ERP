using System;

namespace Domain.Features.Procurement.Approval;

public enum ApprovalDecision
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public class ApprovalAuditRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }

    public Guid ActorUserId { get; set; }

    public string ActorUserName { get; set; } = string.Empty;

    public ApprovalDecision Decision { get; set; }

    public bool IsAutomated { get; set; }

    public DateTime DecisionDateUtc { get; set; } = DateTime.UtcNow;

    public string? Remarks { get; set; }

    public decimal? SnapshotAmount { get; set; }

    public static ApprovalAuditRecord Create(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        ApprovalDecision decision,
        bool isAutomated = false,
        string? remarks = null,
        decimal? snapshotAmount = null)
    {
        return new ApprovalAuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = documentType,
            DocumentId = documentId,
            ActorUserId = actorUserId,
            ActorUserName = actorUserName,
            Decision = decision,
            IsAutomated = isAutomated,
            DecisionDateUtc = DateTime.UtcNow,
            Remarks = remarks,
            SnapshotAmount = snapshotAmount
        };
    }
}
