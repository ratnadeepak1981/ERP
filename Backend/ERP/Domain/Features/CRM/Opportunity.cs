using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.CRM;

public class Opportunity : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid OpportunityId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid? BranchId { get; private set; }

    public string OpportunityNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Guid? LeadId { get; private set; }

    public OpportunityStage Stage { get; private set; } = OpportunityStage.Prospecting;

    public decimal ProbabilityPercent { get; private set; }

    public decimal EstimatedValue { get; private set; }

    public string Currency { get; private set; } = "USD";

    public DateTime? ExpectedCloseDate { get; private set; }

    public DateTime? ActualCloseDate { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public string? LossReason { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    // Marketing Attribution
    public string? CampaignSource { get; private set; }

    public string? CampaignMedium { get; private set; }

    public string? CampaignCode { get; private set; }

    public static Opportunity Create(
        Guid tenantId,
        Guid companyId,
        Guid customerId,
        string opportunityNumber,
        string title,
        decimal estimatedValue,
        Guid? branchId = null,
        Guid? leadId = null,
        OpportunityStage stage = OpportunityStage.Prospecting,
        decimal probabilityPercent = 10,
        string currency = "USD",
        DateTime? expectedCloseDate = null,
        Guid? assignedToUserId = null,
        string? description = null,
        string? campaignSource = null,
        string? campaignMedium = null,
        string? campaignCode = null,
        Guid? opportunityId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId cannot be empty.", nameof(companyId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(opportunityNumber)) throw new ArgumentException("OpportunityNumber is required.", nameof(opportunityNumber));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (estimatedValue < 0) throw new ArgumentOutOfRangeException(nameof(estimatedValue), "EstimatedValue cannot be negative.");
        if (probabilityPercent < 0 || probabilityPercent > 100) throw new ArgumentOutOfRangeException(nameof(probabilityPercent), "ProbabilityPercent must be between 0 and 100.");

        return new Opportunity
        {
            OpportunityId = opportunityId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityNumber = opportunityNumber.Trim(),
            Title = title.Trim(),
            EstimatedValue = estimatedValue,
            Stage = stage,
            ProbabilityPercent = probabilityPercent,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            ExpectedCloseDate = expectedCloseDate,
            AssignedToUserId = assignedToUserId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CampaignSource = string.IsNullOrWhiteSpace(campaignSource) ? null : campaignSource.Trim(),
            CampaignMedium = string.IsNullOrWhiteSpace(campaignMedium) ? null : campaignMedium.Trim(),
            CampaignCode = string.IsNullOrWhiteSpace(campaignCode) ? null : campaignCode.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string title,
        decimal estimatedValue,
        string currency,
        DateTime? expectedCloseDate,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (estimatedValue < 0) throw new ArgumentOutOfRangeException(nameof(estimatedValue), "EstimatedValue cannot be negative.");

        Title = title.Trim();
        EstimatedValue = estimatedValue;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        ExpectedCloseDate = expectedCloseDate;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void AssignTo(Guid? userId, Guid? branchId)
    {
        AssignedToUserId = userId;
        BranchId = branchId;
    }

    public void UpdateStage(OpportunityStage newStage, decimal? probabilityPercent = null)
    {
        Stage = newStage;

        if (probabilityPercent.HasValue)
        {
            if (probabilityPercent.Value < 0 || probabilityPercent.Value > 100)
                throw new ArgumentOutOfRangeException(nameof(probabilityPercent), "ProbabilityPercent must be between 0 and 100.");
            ProbabilityPercent = probabilityPercent.Value;
        }
        else
        {
            // Default stage probabilities
            ProbabilityPercent = newStage switch
            {
                OpportunityStage.Prospecting => 10,
                OpportunityStage.Qualification => 25,
                OpportunityStage.NeedsAnalysis => 50,
                OpportunityStage.Proposal => 75,
                OpportunityStage.Negotiation => 90,
                OpportunityStage.ClosedWon => 100,
                OpportunityStage.ClosedLost => 0,
                _ => ProbabilityPercent
            };
        }

        if (newStage == OpportunityStage.ClosedWon || newStage == OpportunityStage.ClosedLost)
        {
            ActualCloseDate = DateTime.UtcNow;
        }
    }

    public void CloseWon()
    {
        UpdateStage(OpportunityStage.ClosedWon, 100);
        LossReason = null;
    }

    public void CloseLost(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Loss reason is required.", nameof(reason));
        }

        UpdateStage(OpportunityStage.ClosedLost, 0);
        LossReason = reason.Trim();
    }

    public void SetMarketingAttribution(string? source, string? medium, string? code)
    {
        CampaignSource = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        CampaignMedium = string.IsNullOrWhiteSpace(medium) ? null : medium.Trim();
        CampaignCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Opportunity()
    {
    }
}
