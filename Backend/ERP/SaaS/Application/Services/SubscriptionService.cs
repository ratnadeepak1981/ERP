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

        // Commercial vs Free plan initial status:
        // Only the explicit "FREE" shared plan activates immediately.
        // All other plans (including DEDICATED and commercial paid plans) require onboarding/payment verification
        // and start as PendingPayment.
        bool isInstantFreePlan = plan.Code.Equals("FREE", StringComparison.OrdinalIgnoreCase);
        SubscriptionStatus initialStatus = isInstantFreePlan 
            ? SubscriptionStatus.Active 
            : SubscriptionStatus.PendingPayment;

        bool initialIsActive = SubscriptionRules.DeriveIsActive(initialStatus) 
                               && SubscriptionRules.IsActive(startDate, null);

        Subscription subscription = new Subscription
        {
            Id = Guid.NewGuid(),

            TenantId = tenantId,

            SubscriptionPlanId = plan.Id,

            SubscriptionName = subscriptionName,

            SubscriptionType = subscriptionType,

            Status = initialStatus,

            StorageMode = TenantStorageMode.Shared,

            StartDate = startDate,

            EndDate = null,

            IsActive = initialIsActive
        };

        _dbContext.Subscriptions.Add(subscription);

        _dbContext.SaveChanges();

        return subscription;
    }

    public bool IsSubscriptionActive(Guid tenantId)
    {
        Subscription? subscription = _dbContext.Subscriptions
            .FirstOrDefault(x => x.TenantId == tenantId && x.IsActive);

        if (subscription == null)
            return false;

        return SubscriptionRules.IsEntitledToService(
            subscription.Status,
            subscription.StartDate,
            subscription.EndDate,
            DateTime.UtcNow,
            SubscriptionRules.DefaultGracePeriodDays);
    }

    public bool IsWithinLimit(
        Guid tenantId,
        string parameterKey,
        decimal currentValue)
    {
        throw new NotImplementedException();
    }
}