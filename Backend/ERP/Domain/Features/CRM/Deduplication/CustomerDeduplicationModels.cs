using System;

namespace ERP.Domain.Features.CRM.Deduplication;

public enum CustomerMatchConfidence
{
    None = 0,
    ExactMatch = 1,
    PotentialMatch = 2
}

public enum CustomerMatchType
{
    None = 0,
    TaxNumberExact = 1,
    EmailExact = 2,
    PhoneExact = 3,
    CustomerNameNormalized = 4
}

public class CustomerMatchCriteria
{
    public string? TaxNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CustomerName { get; set; }

    public static CustomerMatchCriteria FromLead(Lead lead)
    {
        if (lead == null) throw new ArgumentNullException(nameof(lead));

        return new CustomerMatchCriteria
        {
            TaxNumber = null,
            Email = lead.Email,
            Phone = lead.Phone,
            CustomerName = lead.CompanyName ?? lead.ContactName
        };
    }
}

public class CustomerMatchResult
{
    public CustomerMatchConfidence Confidence { get; set; }

    public CustomerMatchType MatchType { get; set; }

    public Guid? MatchedCustomerId { get; set; }

    public string? MatchedCustomerCode { get; set; }

    public string? MatchedCustomerName { get; set; }

    public string? MatchReason { get; set; }

    public bool IsDuplicate => Confidence == CustomerMatchConfidence.ExactMatch;

    public bool HasPotentialDuplicate => Confidence != CustomerMatchConfidence.None;

    public static CustomerMatchResult NoMatch() => new()
    {
        Confidence = CustomerMatchConfidence.None,
        MatchType = CustomerMatchType.None
    };

    public static CustomerMatchResult Exact(
        CustomerMatchType type,
        Guid customerId,
        string code,
        string name,
        string reason) => new()
    {
        Confidence = CustomerMatchConfidence.ExactMatch,
        MatchType = type,
        MatchedCustomerId = customerId,
        MatchedCustomerCode = code,
        MatchedCustomerName = name,
        MatchReason = reason
    };

    public static CustomerMatchResult Potential(
        CustomerMatchType type,
        Guid customerId,
        string code,
        string name,
        string reason) => new()
    {
        Confidence = CustomerMatchConfidence.PotentialMatch,
        MatchType = type,
        MatchedCustomerId = customerId,
        MatchedCustomerCode = code,
        MatchedCustomerName = name,
        MatchReason = reason
    };
}
