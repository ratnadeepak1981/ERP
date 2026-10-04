using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionService
{
    Subscription CreateDefaultSubscription(Guid tenantId);

    bool IsSubscriptionActive(Guid tenantId);

    bool IsWithinLimit( Guid tenantId, string parameterKey, decimal currentValue);
}