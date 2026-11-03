using System;

namespace API.Common.TenantResolution;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? Hostname { get; private set; }
    public string? Subdomain { get; private set; }
    public bool IsTenantDomainRequest { get; private set; }
    public bool IsLegacySharedHost { get; private set; }

    public void SetResolvedTenant(Guid tenantId, string hostname, string? subdomain)
    {
        TenantId = tenantId;
        Hostname = hostname;
        Subdomain = subdomain;
        IsTenantDomainRequest = true;
        IsLegacySharedHost = false;
    }

    public void SetLegacySharedHost(string hostname)
    {
        TenantId = null;
        Hostname = hostname;
        Subdomain = null;
        IsTenantDomainRequest = false;
        IsLegacySharedHost = true;
    }
}
