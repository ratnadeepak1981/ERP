using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionService
{
    Subscription CreateSubscription(
        Guid tenantId,
        Guid subscriptionPlanId);

    bool IsSubscriptionActive(Guid tenantId);

    bool IsWithinLimit(
        Guid tenantId,
        string parameterKey,
        decimal currentValue);
}