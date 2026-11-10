using Microsoft.EntityFrameworkCore;
using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using System;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;

namespace SaaS.Application.Services;

public class TenantService : ITenantService
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionUsageService _subscriptionUsageService;

    public TenantService(
        ISubscriptionService subscriptionService,
        ITenantRepository tenantRepository,
        ISubscriptionUsageService subscriptionUsageService)
    {
        _subscriptionService = subscriptionService;
        _tenantRepository = tenantRepository;
        _subscriptionUsageService = subscriptionUsageService;
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
            Subdomain = TenantSlugRules.GenerateSlug(name),
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

        // Subdomain generation / validation
        string finalSubdomain;
        if (!string.IsNullOrWhiteSpace(request.Subdomain))
        {
            string requestedSlug = TenantSlugRules.GenerateSlug(request.Subdomain);
            TenantSlugRules.ValidateSubdomain(requestedSlug);

            if (await _tenantRepository.ExistsBySubdomainAsync(requestedSlug))
            {
                throw new InvalidOperationException($"The subdomain '{requestedSlug}' is already taken.");
            }

            finalSubdomain = requestedSlug;
        }
        else
        {
            string baseSlug = TenantSlugRules.GenerateSlug(request.Name);
            TenantSlugRules.ValidateSubdomain(baseSlug);

            finalSubdomain = await TenantSlugRules.ResolveCollisionAsync(
                baseSlug,
                _tenantRepository.ExistsBySubdomainAsync);
        }

        // Company structure validation (Single or Multi)
        string structure = string.IsNullOrWhiteSpace(request.CompanyStructure)
            ? "Single"
            : request.CompanyStructure.Trim();

        if (!string.Equals(structure, "Single", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(structure, "Multi", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Company structure must be 'Single' or 'Multi'.", nameof(request.CompanyStructure));
        }

        Guid tenantId = Guid.NewGuid();

        string tenantCode =
            $"TEN-{tenantId.ToString("N")[..8].ToUpperInvariant()}";

        Tenant tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name.Trim(),
            Code = tenantCode,
            Subdomain = finalSubdomain,
            IndustryType = string.IsNullOrWhiteSpace(request.IndustryType) ? null : request.IndustryType.Trim(),
            CompanyStructure = structure,
            ECommerceEnabled = request.ECommerceEnabled,
            AdminFullName = string.IsNullOrWhiteSpace(request.AdminFullName) ? null : request.AdminFullName.Trim(),
            AdminEmail = string.IsNullOrWhiteSpace(request.AdminEmail) ? null : request.AdminEmail.Trim().ToLowerInvariant(),
            AdminPhone = string.IsNullOrWhiteSpace(request.AdminPhone) ? null : request.AdminPhone.Trim(),
            IsActive = true
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

            await _tenantRepository.SaveChangesAsync();

            await _tenantRepository.CommitTransactionAsync(
                transaction);

            return new TenantRegistrationResult
            {
                Tenant = tenant,
                Subscription = subscription
            };
        }
        catch
        {
            await _tenantRepository.RollbackTransactionAsync(
                transaction);

            throw;
        }
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
