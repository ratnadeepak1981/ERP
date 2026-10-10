using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.Approval;

public interface IApprovalWorkflowService
{
    Task<ProcurementApprovalSettings> GetApprovalSettingsAsync(Guid tenantId);

    Task RecordSubmissionAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount);

    Task RecordAutoApprovalAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount);

    Task RecordApprovalAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid approverUserId,
        string approverUserName,
        Guid creatorUserId,
        decimal snapshotAmount,
        string? remarks = null);

    Task RecordRejectionAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid approverUserId,
        string approverUserName,
        string remarks,
        decimal snapshotAmount);

    Task RecordCancellationAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount,
        string? remarks = null);

    Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        string documentType,
        Guid documentId);
}
