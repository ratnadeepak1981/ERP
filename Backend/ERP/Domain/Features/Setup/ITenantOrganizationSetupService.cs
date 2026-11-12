using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Setup;

public class TenantOrganizationSetupRequest
{
    public string? CompanyName { get; set; }
    public string? CompanyCode { get; set; }
    public string? BaseCurrencyCode { get; set; }
    public string? BranchName { get; set; }
    public string? BranchCode { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryName { get; set; }
}

public class TenantOrganizationSetupResult
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool CompanyCreated { get; set; }

    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool BranchCreated { get; set; }

    public Guid? CompanyAddressId { get; set; }
    public bool CompanyAddressCreated { get; set; }

    public Guid? BranchAddressId { get; set; }
    public bool BranchAddressCreated { get; set; }

    public List<string> Messages { get; set; } = new();
}

public interface ITenantOrganizationSetupService
{
    Task<TenantOrganizationSetupResult> SetupOrganizationAsync(
        Guid tenantId,
        TenantOrganizationSetupRequest? request = null);
}
