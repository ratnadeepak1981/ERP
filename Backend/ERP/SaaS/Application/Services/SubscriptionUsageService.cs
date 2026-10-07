using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class SubscriptionUsageService
    : ISubscriptionUsageService
{
    private readonly ISubscriptionRepository
        _subscriptionRepository;

    private readonly ISubscriptionUsageRepository
        _usageRepository;

    public SubscriptionUsageService(
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionUsageRepository usageRepository)
    {
        _subscriptionRepository =
            subscriptionRepository;

        _usageRepository =
            usageRepository;
    }

    public async Task InitializeUsageAsync(
        Subscription subscription)
    {
        IReadOnlyList<SubscriptionPlanParameter> parameters =
            await _subscriptionRepository
                .GetActivePlanParametersAsync(
                    subscription.SubscriptionPlanId);

        DateTime periodStart =
            subscription.StartDate;

        DateTime periodEnd =
            subscription.EndDate
            ?? DateTime.MaxValue;

        foreach (SubscriptionPlanParameter parameter in parameters)
        {
            SubscriptionUsage usage =
                new SubscriptionUsage
                {
                    Id = Guid.NewGuid(),

                    SubscriptionId =
                        subscription.Id,

                    SubscriptionPlanParameterId =
                        parameter.Id,

                    UsageValue = 0,

                    PeriodStart =
                        periodStart,

                    PeriodEnd =
                        periodEnd,

                    LastUpdated =
                        DateTime.UtcNow
                };

            await _usageRepository.AddAsync(
                usage);
        }

        await _usageRepository.SaveChangesAsync();
    }
}