using SaaS.Application.Interfaces.Repositories;
using SaaS.Bootstrap.Subscription;

namespace SaaS.Bootstrap;

public static class SaaSBootstrap
{
    public static void Seed(
        ISubscriptionPlanRepository planRepository,
        ISubscriptionParameterRepository parameterRepository,
        ISubscriptionLimitRepository limitRepository,
        ISubscriptionPlanParameterRepository planParameterRepository)
    {
        SubscriptionSeed.Seed(
            planRepository,
            parameterRepository,
            limitRepository,
            planParameterRepository);
    }
}