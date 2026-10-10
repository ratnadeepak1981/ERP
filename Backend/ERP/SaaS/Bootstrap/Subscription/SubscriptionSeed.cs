
using SaaS.Application.Interfaces.Repositories;
using SaaS.Common;
using SaaS.Core.Models;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;

namespace SaaS.Bootstrap.Subscription;

public static class SubscriptionSeed
{
    public static void Seed(
        ISubscriptionPlanRepository planRepository,
        ISubscriptionParameterRepository parameterRepository,
        ISubscriptionLimitRepository limitRepository,
        ISubscriptionPlanParameterRepository planParameterRepository)
    {
        SeedPlans(planRepository);
        SeedParameters(parameterRepository);

        planRepository.SaveChanges();
        parameterRepository.SaveChanges();

        SeedLimits(
            planRepository,
            limitRepository);

        limitRepository.SaveChanges();

        SeedPlanParameters(
            planRepository,
            parameterRepository,
            limitRepository,
            planParameterRepository);

        planParameterRepository.SaveChanges();
    }

    // =========================
    // Plans
    // =========================

    private static void SeedPlans(
        ISubscriptionPlanRepository repository)
    {
        SeedPlan(
            repository,
            "FREE",
            "Free",
            "Free shared tenant subscription plan.");

        SeedPlan(
            repository,
            "DEDICATED",
            "Dedicated",
            "Dedicated tenant subscription plan.");
    }

    private static void SeedPlan(
        ISubscriptionPlanRepository repository,
        string code,
        string name,
        string description)
    {
        if (repository.ExistsByCode(code))
            return;

        repository.Add(new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            Price = 0m,
            BillingCycle = DurationCycle.Monthly,
            IsActive = true
        });
    }

    // =========================
    // Parameters
    // =========================

    private static void SeedParameters(
        ISubscriptionParameterRepository repository)
    {
        SeedParameter(repository, "USERS");
        SeedParameter(repository, "MASTER_DATA");
        SeedParameter(repository, "TRANSACTIONS");
        SeedParameter(repository, "COMPANIES");
        SeedParameter(repository, "BRANCHES");
    }

    private static void SeedParameter(
        ISubscriptionParameterRepository repository,
        string parameterKey)
    {
        if (repository.ExistsByKey(parameterKey))
            return;

        repository.Add(new SubscriptionParameter
        {
            Id = Guid.NewGuid(),
            ParameterKey = parameterKey,
            ParameterType = parameterKey == "TRANSACTIONS"
                ? SubscriptionParameterType.Usage
                : SubscriptionParameterType.Fixed,
            IsActive = true
        });
    }

    // =========================
    // Subscription Limits
    // =========================

    private static void SeedLimits(
        ISubscriptionPlanRepository planRepository,
        ISubscriptionLimitRepository limitRepository)
    {
        var plans = planRepository.GetAll();

        foreach (var plan in plans)
        {
            if (limitRepository.ExistsByPlanId(plan.Id))
                continue;

            limitRepository.Add(new SubscriptionLimit
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = plan.Id,
                IsActive = true
            });
        }
    }

    // =========================
    // Plan Parameters
    // =========================

    private static void SeedPlanParameters(
        ISubscriptionPlanRepository planRepository,
        ISubscriptionParameterRepository parameterRepository,
        ISubscriptionLimitRepository limitRepository,
        ISubscriptionPlanParameterRepository planParameterRepository)
    {
        var freePlan =
            planRepository.GetAll()
                .FirstOrDefault(x => x.Code == "FREE");

        var dedicatedPlan =
            planRepository.GetAll()
                .FirstOrDefault(x => x.Code == "DEDICATED");

        if (freePlan == null || dedicatedPlan == null)
            throw new InvalidOperationException(
                "Required subscription plans do not exist.");

        var parameters = parameterRepository.GetAll();

        var freeLimit =
            limitRepository.GetAll()
                .First(x => x.SubscriptionPlanId == freePlan.Id);

        var dedicatedLimit =
            limitRepository.GetAll()
                .First(x => x.SubscriptionPlanId == dedicatedPlan.Id);

        // FREE
        SeedPlanParameter(
            planParameterRepository,
            freeLimit.Id,
            parameters,
            "USERS",
            5m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            freeLimit.Id,
            parameters,
            "MASTER_DATA",
            1000m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            freeLimit.Id,
            parameters,
            "TRANSACTIONS",
            10000m,
            DurationCycle.Monthly);

        SeedPlanParameter(
            planParameterRepository,
            freeLimit.Id,
            parameters,
            "COMPANIES",
            1m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            freeLimit.Id,
            parameters,
            "BRANCHES",
            2m,
            DurationCycle.Lifetime);

        // DEDICATED
        SeedPlanParameter(
            planParameterRepository,
            dedicatedLimit.Id,
            parameters,
            "USERS",
            100m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            dedicatedLimit.Id,
            parameters,
            "MASTER_DATA",
            100000m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            dedicatedLimit.Id,
            parameters,
            "TRANSACTIONS",
            1000000m,
            DurationCycle.Monthly);

        SeedPlanParameter(
            planParameterRepository,
            dedicatedLimit.Id,
            parameters,
            "COMPANIES",
            10m,
            DurationCycle.Lifetime);

        SeedPlanParameter(
            planParameterRepository,
            dedicatedLimit.Id,
            parameters,
            "BRANCHES",
            50m,
            DurationCycle.Lifetime);
    }

    private static void SeedPlanParameter(
        ISubscriptionPlanParameterRepository repository,
        Guid subscriptionLimitId,
        IReadOnlyList<SubscriptionParameter> parameters,
        string parameterKey,
        decimal limitValue,
        DurationCycle duration)
    {
        var parameter =
            parameters.FirstOrDefault(
                x => x.ParameterKey == parameterKey);

        if (parameter == null)
            throw new InvalidOperationException(
                $"Subscription parameter '{parameterKey}' does not exist.");

        if (repository.Exists(
                subscriptionLimitId,
                parameter.Id))
            return;

        repository.Add(new SubscriptionPlanParameter
        {
            Id = Guid.NewGuid(),
            SubscriptionLimitId = subscriptionLimitId,
            SubscriptionParameterId = parameter.Id,
            LimitValue = limitValue,
            IsUnlimited = false,
            Duration = duration,
            IsActive = true
        });
    }
}
