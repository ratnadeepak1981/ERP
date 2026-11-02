using System;
using System.Threading;
using System.Threading.Tasks;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Rules;

namespace SaaS.Application.Services;

public class TenantDomainService : ITenantDomainService
{
    private readonly ITenantDomainRepository _tenantDomainRepository;

    public TenantDomainService(ITenantDomainRepository tenantDomainRepository)
    {
        _tenantDomainRepository = tenantDomainRepository;
    }

    public async Task<TenantResolutionResult> ResolveTenantByHostnameAsync(
        string hostname,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return TenantResolutionResult.Malformed("Hostname is empty or whitespace.");
        }

        string normalized = TenantDomainRules.NormalizeHostname(hostname);

        if (!TenantDomainRules.IsValidHostname(normalized))
        {
            return TenantResolutionResult.Malformed($"Hostname '{hostname}' is malformed.");
        }

        var domain = await _tenantDomainRepository.GetByHostnameAsync(normalized);
        if (domain == null)
        {
            return TenantResolutionResult.NotFound($"Tenant domain '{normalized}' is not registered.");
        }

        if (!domain.IsActive)
        {
            return TenantResolutionResult.InactiveDomain(
                domain.TenantId,
                domain.Hostname,
                domain.Subdomain,
                domain.DomainType);
        }

        if (!domain.IsVerified)
        {
            return TenantResolutionResult.UnverifiedDomain(
                domain.TenantId,
                domain.Hostname,
                domain.Subdomain,
                domain.DomainType);
        }

        if (domain.Tenant == null || !domain.Tenant.IsActive)
        {
            return TenantResolutionResult.InactiveTenant(
                domain.TenantId,
                domain.Hostname,
                domain.Subdomain,
                domain.DomainType);
        }

        return TenantResolutionResult.Success(
            domain.TenantId,
            domain.Hostname,
            domain.Subdomain,
            domain.DomainType,
            domain.IsPrimary);
    }
}
