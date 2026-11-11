using System;

namespace API.Common.TenantResolution;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Hostname { get; }
    string? Subdomain { get; }
    string? TenantName { get; }
    string? IndustryType { get; }
    string? CompanyStructure { get; }
    bool ECommerceEnabled { get; }
    bool IsTenantDomainRequest { get; }
    bool IsLegacySharedHost { get; }

    void SetResolvedTenant(Guid tenantId, string hostname, string? subdomain, string? tenantName = null, string? industryType = null, string? companyStructure = null, bool eCommerceEnabled = false);
    void SetLegacySharedHost(string hostname);
}
