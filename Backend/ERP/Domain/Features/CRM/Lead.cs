using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.CRM;

public class Lead : Auditable, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid LeadId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid? BranchId { get; private set; }

    public string LeadNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? CompanyName { get; private set; }

    public string ContactName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public LeadSource LeadSource { get; private set; } = LeadSource.DirectInquiry;

    public LeadStatus Status { get; private set; } = LeadStatus.New;

    public Guid? AssignedToUserId { get; private set; }

    public decimal EstimatedValue { get; private set; }

    public string Currency { get; private set; } = "USD";

    // Marketing & Attribution (Nullable/Optional for future Revenue Growth Intelligence)
    public string? CampaignSource { get; private set; }

    public string? CampaignMedium { get; private set; }

    public string? CampaignCode { get; private set; }

    public string? ReferrerUrl { get; private set; }

    // Lifecycle timestamps & outcomes
    public DateTime? QualifiedAt { get; private set; }

    public DateTime? DisqualifiedAt { get; private set; }

    public string? DisqualificationReason { get; private set; }

    public Guid? ConvertedCustomerId { get; private set; }

    public DateTime? ConvertedAt { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static Lead Create(
        Guid tenantId,
        Guid companyId,
        string leadNumber,
        string title,
        string contactName,
        Guid? branchId = null,
        string? companyName = null,
        string? email = null,
        string? phone = null,
        LeadSource leadSource = LeadSource.DirectInquiry,
        Guid? assignedToUserId = null,
        decimal estimatedValue = 0,
        string currency = "USD",
        string? campaignSource = null,
        string? campaignMedium = null,
        string? campaignCode = null,
        string? referrerUrl = null,
        string? notes = null,
        Guid? leadId = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId cannot be empty.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(leadNumber)) throw new ArgumentException("LeadNumber is required.", nameof(leadNumber));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(contactName)) throw new ArgumentException("ContactName is required.", nameof(contactName));
        if (estimatedValue < 0) throw new ArgumentOutOfRangeException(nameof(estimatedValue), "EstimatedValue cannot be negative.");

        return new Lead
        {
            LeadId = leadId ?? Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            LeadNumber = leadNumber.Trim(),
            Title = title.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim(),
            ContactName = contactName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            LeadSource = leadSource,
            Status = LeadStatus.New,
            AssignedToUserId = assignedToUserId,
            EstimatedValue = estimatedValue,
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            CampaignSource = string.IsNullOrWhiteSpace(campaignSource) ? null : campaignSource.Trim(),
            CampaignMedium = string.IsNullOrWhiteSpace(campaignMedium) ? null : campaignMedium.Trim(),
            CampaignCode = string.IsNullOrWhiteSpace(campaignCode) ? null : campaignCode.Trim(),
            ReferrerUrl = string.IsNullOrWhiteSpace(referrerUrl) ? null : referrerUrl.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsActive = true
        };
    }

    public void Update(
        string title,
        string contactName,
        string? companyName,
        string? email,
        string? phone,
        LeadSource leadSource,
        decimal estimatedValue,
        string currency,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(contactName)) throw new ArgumentException("ContactName is required.", nameof(contactName));
        if (estimatedValue < 0) throw new ArgumentOutOfRangeException(nameof(estimatedValue), "EstimatedValue cannot be negative.");

        Title = title.Trim();
        ContactName = contactName.Trim();
        CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        LeadSource = leadSource;
        EstimatedValue = estimatedValue;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void AssignTo(Guid? userId, Guid? branchId)
    {
        AssignedToUserId = userId;
        BranchId = branchId;
    }

    public void SetMarketingAttribution(string? source, string? medium, string? code, string? referrerUrl)
    {
        CampaignSource = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        CampaignMedium = string.IsNullOrWhiteSpace(medium) ? null : medium.Trim();
        CampaignCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        ReferrerUrl = string.IsNullOrWhiteSpace(referrerUrl) ? null : referrerUrl.Trim();
    }

    public void MarkContacted()
    {
        if (Status == LeadStatus.New)
        {
            Status = LeadStatus.Contacted;
        }
    }

    public void Qualify()
    {
        if (Status == LeadStatus.Converted)
        {
            throw new InvalidOperationException($"Converted lead '{LeadNumber}' cannot be re-qualified.");
        }

        Status = LeadStatus.Qualified;
        QualifiedAt = DateTime.UtcNow;
        DisqualifiedAt = null;
        DisqualificationReason = null;
    }

    public void Disqualify(string reason)
    {
        if (Status == LeadStatus.Converted)
        {
            throw new InvalidOperationException($"Converted lead '{LeadNumber}' cannot be disqualified.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Disqualification reason is required.", nameof(reason));
        }

        Status = LeadStatus.Disqualified;
        DisqualifiedAt = DateTime.UtcNow;
        DisqualificationReason = reason.Trim();
    }

    public void ConvertToCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        if (Status == LeadStatus.Converted)
        {
            throw new InvalidOperationException($"Lead '{LeadNumber}' is already converted to customer '{ConvertedCustomerId}'.");
        }

        Status = LeadStatus.Converted;
        ConvertedCustomerId = customerId;
        ConvertedAt = DateTime.UtcNow;
        QualifiedAt ??= DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status == LeadStatus.Converted)
        {
            throw new InvalidOperationException($"Converted lead '{LeadNumber}' cannot be reopened.");
        }

        Status = LeadStatus.New;
        DisqualifiedAt = null;
        DisqualificationReason = null;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Lead()
    {
    }
}
