using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionService(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Subscription CreateSubscription(
        Guid tenantId,
        Guid subscriptionPlanId)
    {
        SubscriptionPlan? plan =
            _dbContext.SubscriptionPlans
                .FirstOrDefault(x =>
                    x.Id == subscriptionPlanId &&
                    x.IsActive);

        if (plan == null)
        {
            throw new InvalidOperationException(
                "Selected subscription plan does not exist or is inactive.");
        }

        string subscriptionName = plan.Name;
        string subscriptionType = plan.Code;

        if (!SubscriptionRules.IsValidSubscriptionName(
                subscriptionName))
        {
            throw new InvalidOperationException(
                "Subscription name is invalid.");
        }

        if (!SubscriptionRules.IsValidSubscriptionType(
                subscriptionType))
        {
            throw new InvalidOperationException(
                "Subscription type is invalid.");
        }

        DateTime startDate = DateTime.UtcNow;

        Subscription subscription = new Subscription
        {
            Id = Guid.NewGuid(),

            TenantId = tenantId,

            SubscriptionPlanId = plan.Id,

            SubscriptionName = subscriptionName,

            SubscriptionType = subscriptionType,

            StorageMode = TenantStorageMode.Shared,

            StartDate = startDate,

            EndDate = null,

            IsActive = SubscriptionRules.IsActive(
                startDate,
                null)
        };

        _dbContext.Subscriptions.Add(subscription);

        _dbContext.SaveChanges();

        return subscription;
    }

    public bool IsSubscriptionActive(Guid tenantId)
    {
        throw new NotImplementedException();
    }

    public bool IsWithinLimit(
        Guid tenantId,
        string parameterKey,
        decimal currentValue)
    {
        throw new NotImplementedException();
    }
}