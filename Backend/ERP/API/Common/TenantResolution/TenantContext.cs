using System;

namespace API.Common.TenantResolution;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? Hostname { get; private set; }
    public string? Subdomain { get; private set; }
    public string? TenantName { get; private set; }
    public string? IndustryType { get; private set; }
    public string? CompanyStructure { get; private set; }
    public bool ECommerceEnabled { get; private set; }
    public bool IsTenantDomainRequest { get; private set; }
    public bool IsLegacySharedHost { get; private set; }

    public void SetResolvedTenant(
        Guid tenantId,
        string hostname,
        string? subdomain,
        string? tenantName = null,
        string? industryType = null,
        string? companyStructure = null,
        bool eCommerceEnabled = false)
    {
        TenantId = tenantId;
        Hostname = hostname;
        Subdomain = subdomain;
        TenantName = tenantName;
        IndustryType = industryType;
        CompanyStructure = companyStructure;
        ECommerceEnabled = eCommerceEnabled;
        IsTenantDomainRequest = true;
        IsLegacySharedHost = false;
    }

    public void SetLegacySharedHost(string hostname)
    {
        TenantId = null;
        Hostname = hostname;
        Subdomain = null;
        TenantName = null;
        IndustryType = null;
        CompanyStructure = null;
        ECommerceEnabled = false;
        IsTenantDomainRequest = false;
        IsLegacySharedHost = true;
    }
}
