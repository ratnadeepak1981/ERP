using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;

namespace SaaS.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    public Subscription CreateDefaultSubscription(
        Guid tenantId)
    {
        string subscriptionName = SubscriptionRules.GetDefaultSubscriptionName();

        string subscriptionType = SubscriptionRules.GetDefaultSubscriptionType();

        if (!SubscriptionRules.IsValidSubscriptionName(subscriptionName))
        {
            throw new InvalidOperationException("Default subscription name is invalid.");
        }

        if (!SubscriptionRules.IsValidSubscriptionType(
                subscriptionType))
        {
            throw new InvalidOperationException(
                "Default subscription type is invalid.");
        }

        DateTime startDate = DateTime.UtcNow;

        return new Subscription
        {
            Id = Guid.NewGuid(),

            TenantId = tenantId,

            SubscriptionName = subscriptionName,

            SubscriptionType = subscriptionType,

            StartDate = startDate,

            EndDate = null,

            IsActive = SubscriptionRules.IsActive(
                startDate,
                null)
        };
    }

    public bool IsSubscriptionActive(Guid tenantId)
    {
        // PlatformDB persistence will be connected later.
        throw new NotImplementedException();
    }

    public bool IsWithinLimit(Guid tenantId,string parameterKey,decimal currentValue)
    {
        // PlatformDB persistence will be connected later.
        throw new NotImplementedException();
    }
}