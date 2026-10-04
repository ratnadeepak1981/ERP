using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;

namespace SaaS.Application.Services;

public class TenantService : ITenantService
{
    private readonly ISubscriptionService _subscriptionService;

    public TenantService(
        ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public Tenant CreateTenant(string name, string code)
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

    public TenantRegistrationResult RegisterTenant(
        CreateTenantRequest request)
    {
        if (!TenantRules.IsValidName(request.Name))
        {
            throw new ArgumentException(
                "Tenant name is required.",
                nameof(request.Name));
        }

        Guid tenantId = Guid.NewGuid();

        string tenantCode =
            $"TEN-{tenantId.ToString("N")[..8].ToUpperInvariant()}";

        Tenant tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name.Trim(),
            Code = tenantCode,
            IsActive = true
        };

        Subscription subscription =
            _subscriptionService.CreateDefaultSubscription(
                tenant.Id);

        return new TenantRegistrationResult
        {
            Tenant = tenant,
            Subscription = subscription
        };
    }
}