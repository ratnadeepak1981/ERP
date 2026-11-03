using SaaS.Core.Models;

namespace SaaS.Application.DTOs;

public class TenantRegistrationResult
{
    public Tenant Tenant { get; set; } = null!;

    public Subscription Subscription { get; set; } = null!;

    public TenantDomain? PrimaryDomain { get; set; }

    public string? Hostname { get; set; }

    public string? Subdomain { get; set; }
}
