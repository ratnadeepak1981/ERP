using System;

namespace ERP.Domain.Features.CRM;

public class OpportunityDto
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string OpportunityNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public OpportunityStage Stage { get; set; }
    public decimal ProbabilityPercent { get; set; }
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpectedCloseDate { get; set; }
    public DateTime? ActualCloseDate { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? LossReason { get; set; }
    public string? Description { get; set; }
    public string? CampaignSource { get; set; }
    public string? CampaignMedium { get; set; }
    public string? CampaignCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class CreateOpportunityRequest
{
    public string OpportunityNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? BranchId { get; set; }
    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;
    public decimal ProbabilityPercent { get; set; } = 10;
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpectedCloseDate { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? Description { get; set; }
    public string? CampaignSource { get; set; }
    public string? CampaignMedium { get; set; }
    public string? CampaignCode { get; set; }
}

public class UpdateOpportunityRequest
{
    public string Title { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? ExpectedCloseDate { get; set; }
    public string? Description { get; set; }
}

public class UpdateOpportunityStageRequest
{
    public OpportunityStage Stage { get; set; }
    public decimal? ProbabilityPercent { get; set; }
    public string? LossReason { get; set; }
}
