using System;
using SaaS.Common;

namespace SaaS.Core.Models;

public class TenantDomain : Auditable
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Hostname { get; set; } = string.Empty;

    public string? Subdomain { get; set; }

    public TenantDomainType DomainType { get; set; } = TenantDomainType.Subdomain;

    public bool IsPrimary { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public bool IsVerified { get; set; } = false;

    public Tenant? Tenant { get; set; }
}
