using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;

namespace SaaS.Application.Services;

public class TenantService : ITenantService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionUsageService _subscriptionUsageService;
    private readonly ITenantDomainRepository _tenantDomainRepository;

    public TenantService(
        ISubscriptionService subscriptionService,
        ITenantRepository tenantRepository,
        ISubscriptionUsageService subscriptionUsageService,
        ITenantDomainRepository tenantDomainRepository)
    {
        _subscriptionService = subscriptionService;
        _tenantRepository = tenantRepository;
        _subscriptionUsageService = subscriptionUsageService;
        _tenantDomainRepository = tenantDomainRepository;
    }

    public Tenant CreateTenant(
        string name,
        string code)
    {
        if (!TenantRules.IsValidName(name))
        {
            throw new ArgumentException(
                "Tenant name is required.",
                nameof(name));
        }

        if (!TenantRules.IsValidCode(code))
        {
            throw new ArgumentException(
                "Tenant code is required.",
                nameof(code));
        }

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = TenantRules.NormalizeCode(code),
            IsActive = true
        };
    }

    public async Task<TenantRegistrationResult> RegisterTenant(
        CreateTenantRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!TenantRules.IsValidName(request.Name))
        {
            throw new ArgumentException(
                "Tenant name is required.",
                nameof(request.Name));
        }

        if (request.SubscriptionPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Subscription plan is required.",
                nameof(request.SubscriptionPlanId));
        }

        if (!string.IsNullOrWhiteSpace(request.CompanyStructure) &&
            !string.Equals(request.CompanyStructure, "Single", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.CompanyStructure, "Multi", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid company structure. Must be 'Single' or 'Multi'.",
                nameof(request.CompanyStructure));
        }

        Guid tenantId = Guid.NewGuid();

        string targetSubdomain;
        if (!string.IsNullOrWhiteSpace(request.Subdomain))
        {
            if (!TenantDomainRules.IsValidSubdomain(request.Subdomain))
            {
                throw new ArgumentException(
                    $"Subdomain '{request.Subdomain}' is invalid or reserved.",
                    nameof(request.Subdomain));
            }

            targetSubdomain = TenantDomainRules.NormalizeSubdomain(request.Subdomain);

            if (await _tenantDomainRepository.ExistsSubdomainAsync(targetSubdomain))
            {
                throw new InvalidOperationException(
                    $"Subdomain '{targetSubdomain}' is already registered.");
            }
        }
        else
        {
            targetSubdomain = await GenerateAvailableSubdomainAsync(request.Name, tenantId);
        }

        string hostname = TenantDomainRules.BuildLocalhostHostname(targetSubdomain);
        if (await _tenantDomainRepository.ExistsHostnameAsync(hostname))
        {
            throw new InvalidOperationException(
                $"Hostname '{hostname}' is already registered.");
        }

        string tenantCode =
            $"TEN-{tenantId.ToString("N")[..8].ToUpperInvariant()}";

        Tenant tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name.Trim(),
            Code = tenantCode,
            IsActive = true,
            Subdomain = targetSubdomain
        };

        var transaction =
            await _tenantRepository.BeginTransactionAsync();

        try
        {
            await _tenantRepository.AddAsync(tenant);

            Subscription subscription =
                _subscriptionService.CreateSubscription(
                    tenant.Id,
                    request.SubscriptionPlanId);

            await _subscriptionUsageService
                .InitializeUsageAsync(subscription);

            TenantDomain tenantDomain = new TenantDomain
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Hostname = hostname,
                Subdomain = targetSubdomain,
                DomainType = TenantDomainType.Subdomain,
                IsPrimary = true,
                IsActive = true,
                IsVerified = true
            };

            await _tenantDomainRepository.AddAsync(tenantDomain);

            await _tenantRepository.SaveChangesAsync();

            await _tenantRepository.CommitTransactionAsync(
                transaction);

            return new TenantRegistrationResult
            {
                Tenant = tenant,
                Subscription = subscription,
                PrimaryDomain = tenantDomain,
                Hostname = tenantDomain.Hostname,
                Subdomain = tenantDomain.Subdomain
            };
        }
        catch
        {
            await _tenantRepository.RollbackTransactionAsync(
                transaction);

            throw;
        }
    }

    private async Task<string> GenerateAvailableSubdomainAsync(string tenantName, Guid tenantId)
    {
        string rawSlug = TenantSlugRules.GenerateSlug(tenantName);
        if (string.IsNullOrWhiteSpace(rawSlug) || rawSlug.Length < 3 || TenantDomainRules.IsReservedSubdomain(rawSlug))
        {
            rawSlug = $"tenant-{tenantId.ToString("N")[..6]}";
        }

        return await TenantSlugRules.ResolveCollisionAsync(
            rawSlug,
            async candidate =>
                await _tenantDomainRepository.ExistsSubdomainAsync(candidate) ||
                await _tenantRepository.ExistsBySubdomainAsync(candidate) ||
                TenantSlugRules.IsReserved(candidate) ||
                TenantDomainRules.IsReservedSubdomain(candidate));
    }

    public Task<List<TenantQueueItemDto>> GetTenantListAsync()
    {
        return _tenantRepository.GetTenantListAsync();
    }

    public Task<TenantDetailsDto?> GetTenantDetailsAsync(Guid tenantId)
    {
        return _tenantRepository.GetTenantDetailsAsync(tenantId);
    }

    public Task<TenantLifecycleStatusDto?> GetTenantStatusAsync(Guid tenantId)
    {
        return _tenantRepository.GetTenantStatusAsync(tenantId);
    }

    public Task<TenantMetricsDto> GetTenantMetricsAsync()
    {
        return _tenantRepository.GetTenantMetricsAsync();
    }
}
