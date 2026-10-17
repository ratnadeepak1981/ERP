using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.CRM;

public class CrmActivity : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid CrmActivityId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid? BranchId { get; private set; }

    public CrmActivityType ActivityType { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime? DueDate { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public bool IsCompleted { get; private set; }

    public string? CompletionNotes { get; private set; }

    // Associated Entities (At least one must normally be associated)
    public Guid? LeadId { get; private set; }

    public Guid? OpportunityId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static CrmActivity Create(
        Guid tenantId,
        Guid companyId,
        CrmActivityType activityType,
        string subject,
        Guid? branchId = null,
        string? description = null,
        DateTime? dueDate = null,
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        Guid? assignedToUserId = null,
        Guid? crmActivityId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId cannot be empty.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));

        if (leadId == null && opportunityId == null && customerId == null)
        {
            throw new ArgumentException("CrmActivity must be associated with at least one entity (Lead, Opportunity, or Customer).");
        }

        return new CrmActivity
        {
            CrmActivityId = crmActivityId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            ActivityType = activityType,
            Subject = subject.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueDate = dueDate,
            LeadId = leadId,
            OpportunityId = opportunityId,
            CustomerId = customerId,
            AssignedToUserId = assignedToUserId,
            IsCompleted = false,
            IsActive = true
        };
    }

    public void Update(
        CrmActivityType activityType,
        string subject,
        string? description,
        DateTime? dueDate)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));

        ActivityType = activityType;
        Subject = subject.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
    }

    public void AssignTo(Guid? userId, Guid? branchId)
    {
        AssignedToUserId = userId;
        BranchId = branchId;
    }

    public void MarkCompleted(string? completionNotes = null, DateTime? completedAt = null)
    {
        IsCompleted = true;
        CompletedAt = completedAt ?? DateTime.UtcNow;
        CompletionNotes = string.IsNullOrWhiteSpace(completionNotes) ? null : completionNotes.Trim();
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedAt = null;
        CompletionNotes = null;
    }

    public void Reschedule(DateTime? newDueDate)
    {
        DueDate = newDueDate;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private CrmActivity()
    {
    }
}
