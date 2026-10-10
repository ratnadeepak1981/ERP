using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.Approval;

public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly IProcurementApprovalSettingsProvider _settingsProvider;
    private readonly IApprovalAuditRepository _auditRepository;

    public ApprovalWorkflowService(
        IProcurementApprovalSettingsProvider settingsProvider,
        IApprovalAuditRepository auditRepository)
    {
        _settingsProvider = settingsProvider;
        _auditRepository = auditRepository;
    }

    public async Task<ProcurementApprovalSettings> GetApprovalSettingsAsync(Guid tenantId)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(tenantId);
            return settings ?? new ProcurementApprovalSettings { ApprovalRequired = true, ApprovalMode = 1 };
        }
        catch
        {
            // Fail-safe default: Do not bypass approval if settings cannot be retrieved
            return new ProcurementApprovalSettings { ApprovalRequired = true, ApprovalMode = 1 };
        }
    }

    public async Task RecordSubmissionAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount)
    {
        var record = ApprovalAuditRecord.Create(
            tenantId,
            documentType,
            documentId,
            actorUserId,
            actorUserName,
            ApprovalDecision.Submitted,
            isAutomated: false,
            remarks: "Document submitted for approval.",
            snapshotAmount: snapshotAmount);

        await _auditRepository.AddRecordAsync(record);
        await _auditRepository.SaveChangesAsync();
    }

    public async Task RecordAutoApprovalAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount)
    {
        var record = ApprovalAuditRecord.Create(
            tenantId,
            documentType,
            documentId,
            actorUserId,
            actorUserName,
            ApprovalDecision.Approved,
            isAutomated: true,
            remarks: "Automatically approved per tenant approval policy.",
            snapshotAmount: snapshotAmount);

        await _auditRepository.AddRecordAsync(record);
        await _auditRepository.SaveChangesAsync();
    }

    public async Task RecordApprovalAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid approverUserId,
        string approverUserName,
        Guid creatorUserId,
        decimal snapshotAmount,
        string? remarks = null)
    {
        // Enforce self-approval policy: Submitter cannot approve their own transaction
        if (creatorUserId != Guid.Empty && approverUserId == creatorUserId)
        {
            throw new InvalidOperationException("Users are not permitted to approve their own transactions.");
        }

        var record = ApprovalAuditRecord.Create(
            tenantId,
            documentType,
            documentId,
            approverUserId,
            approverUserName,
            ApprovalDecision.Approved,
            isAutomated: false,
            remarks: string.IsNullOrWhiteSpace(remarks) ? "Approved by authorized user." : remarks.Trim(),
            snapshotAmount: snapshotAmount);

        await _auditRepository.AddRecordAsync(record);
        await _auditRepository.SaveChangesAsync();
    }

    public async Task RecordRejectionAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid approverUserId,
        string approverUserName,
        string remarks,
        decimal snapshotAmount)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new ArgumentException("Remarks are required when rejecting a transaction.", nameof(remarks));
        }

        var record = ApprovalAuditRecord.Create(
            tenantId,
            documentType,
            documentId,
            approverUserId,
            approverUserName,
            ApprovalDecision.Rejected,
            isAutomated: false,
            remarks: remarks.Trim(),
            snapshotAmount: snapshotAmount);

        await _auditRepository.AddRecordAsync(record);
        await _auditRepository.SaveChangesAsync();
    }

    public async Task RecordCancellationAsync(
        Guid tenantId,
        string documentType,
        Guid documentId,
        Guid actorUserId,
        string actorUserName,
        decimal snapshotAmount,
        string? remarks = null)
    {
        var record = ApprovalAuditRecord.Create(
            tenantId,
            documentType,
            documentId,
            actorUserId,
            actorUserName,
            ApprovalDecision.Cancelled,
            isAutomated: false,
            remarks: string.IsNullOrWhiteSpace(remarks) ? "Transaction cancelled." : remarks.Trim(),
            snapshotAmount: snapshotAmount);

        await _auditRepository.AddRecordAsync(record);
        await _auditRepository.SaveChangesAsync();
    }

    public async Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        string documentType,
        Guid documentId)
    {
        return await _auditRepository.GetRecordsAsync(tenantId, documentType, documentId);
    }
}
