using System;

namespace ERP.Domain.Features.CRM;

public class CrmActivityDto
{
    public Guid CrmActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public CrmActivityType ActivityType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? CompletionNotes { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class CreateCrmActivityRequest
{
    public CrmActivityType ActivityType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? AssignedToUserId { get; set; }
}

public class UpdateCrmActivityRequest
{
    public CrmActivityType ActivityType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
}

public class CompleteCrmActivityRequest
{
    public string? CompletionNotes { get; set; }
    public DateTime? CompletedAt { get; set; }
}
