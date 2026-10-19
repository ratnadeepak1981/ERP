using SaaS.Common;

namespace SaaS.Core.Models;

public class TenantBillingProfile : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string BillingEmail { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public string Currency { get; set; } = "USD";

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? StateOrProvince { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty; // ISO 3166-1 alpha-2

    public bool IsTaxExempt { get; set; } = false;

    public Tenant Tenant { get; set; } = null!;
}
