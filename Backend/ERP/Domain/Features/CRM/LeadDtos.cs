using System;

namespace ERP.Domain.Features.CRM;

public class LeadDto
{
    public Guid LeadId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource LeadSource { get; set; }
    public LeadStatus Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public string? CampaignSource { get; set; }
    public string? CampaignMedium { get; set; }
    public string? CampaignCode { get; set; }
    public string? ReferrerUrl { get; set; }
    public DateTime? QualifiedAt { get; set; }
    public DateTime? DisqualifiedAt { get; set; }
    public string? DisqualificationReason { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public class CreateLeadRequest
{
    public string LeadNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public Guid? BranchId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource LeadSource { get; set; } = LeadSource.DirectInquiry;
    public Guid? AssignedToUserId { get; set; }
    public decimal EstimatedValue { get; set; } = 0;
    public string Currency { get; set; } = "USD";
    public string? CampaignSource { get; set; }
    public string? CampaignMedium { get; set; }
    public string? CampaignCode { get; set; }
    public string? ReferrerUrl { get; set; }
    public string? Notes { get; set; }
}

public class UpdateLeadRequest
{
    public string Title { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LeadSource LeadSource { get; set; }
    public decimal EstimatedValue { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
}

public class AssignLeadRequest
{
    public Guid? AssignedToUserId { get; set; }
    public Guid? BranchId { get; set; }
}

public class DisqualifyLeadRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ConvertLeadRequest
{
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? TaxNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public bool LinkToExistingIfMatched { get; set; } = true;
}

public class ConvertLeadResult
{
    public Guid LeadId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public bool WasExistingCustomer { get; set; }
    public string MatchReason { get; set; } = string.Empty;
}
