using System;

namespace API.Common.TenantResolution;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Hostname { get; }
    string? Subdomain { get; }
    bool IsTenantDomainRequest { get; }
    bool IsLegacySharedHost { get; }

    void SetResolvedTenant(Guid tenantId, string hostname, string? subdomain);
    void SetLegacySharedHost(string hostname);
}
