using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Repositories;

namespace SaaS.Application.Services;

public class SubscriptionUsageService : ISubscriptionUsageService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionUsageRepository _usageRepository;
    private readonly SaaSDbContext _dbContext;

    public SubscriptionUsageService(
        ISubscriptionRepository subscriptionRepository,
        ISubscriptionUsageRepository usageRepository,
        SaaSDbContext? dbContext = null)
    {
        _subscriptionRepository = subscriptionRepository;
        _usageRepository = usageRepository;
        _dbContext = dbContext
            ?? (subscriptionRepository as SubscriptionRepository)?.Context
            ?? (usageRepository as SubscriptionUsageRepository)?.Context!;
    }

    public SubscriptionUsageService(SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
        _subscriptionRepository = new SubscriptionRepository(dbContext);
        _usageRepository = new SubscriptionUsageRepository(dbContext);
    }

    public async Task InitializeUsageAsync(Subscription subscription)
    {
        IReadOnlyList<SubscriptionPlanParameter> parameters =
            await _subscriptionRepository
                .GetActivePlanParametersAsync(subscription.SubscriptionPlanId);

        DateTime periodStart = subscription.CurrentPeriodStart != default ? subscription.CurrentPeriodStart : subscription.StartDate;
        DateTime periodEnd = subscription.CurrentPeriodEnd != default ? subscription.CurrentPeriodEnd : (subscription.EndDate ?? DateTime.MaxValue);

        foreach (SubscriptionPlanParameter parameter in parameters)
        {
            SubscriptionUsage usage = new SubscriptionUsage
            {
                Id = Guid.NewGuid(),
                SubscriptionId = subscription.Id,
                SubscriptionPlanParameterId = parameter.Id,
                UsageValue = 0,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                LastUpdated = DateTime.UtcNow
            };

            await _usageRepository.AddAsync(usage);
        }

        await _usageRepository.SaveChangesAsync();
    }

    public async Task<T> ExecuteWithUsageLimitAsync<T>(
        Guid tenantId,
        string parameterKey,
        decimal increment,
        Func<Task<T>> createEntityAction,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(parameterKey))
            throw new ArgumentException("Parameter key is required.", nameof(parameterKey));

        if (increment <= 0)
            throw new ArgumentException("Increment must be greater than zero.", nameof(increment));

        if (createEntityAction == null)
            throw new ArgumentNullException(nameof(createEntityAction));

        var isSqlServer = _dbContext.Database.IsSqlServer();
        IDbContextTransaction? transaction = null;

        if (isSqlServer)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            string resourceKey = $"SubLimit_{tenantId:N}_{parameterKey.Trim().ToUpperInvariant()}";
            await _dbContext.Database.ExecuteSqlRawAsync(
                "DECLARE @res int; EXEC @res = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @res < 0 THROW 50000, 'Could not acquire subscription lock', 1;",
                new object[] { resourceKey },
                cancellationToken);
        }

        try
        {
            // 1. Verify tenant active subscription & lifecycle entitlement
            var subscription = await _dbContext.Subscriptions
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.IsActive, cancellationToken);

            if (subscription == null)
            {
                throw new InvalidOperationException("Tenant has no active subscription.");
            }

            bool isEntitled = SubscriptionRules.IsEntitledToService(
                subscription.Status,
                subscription.StartDate,
                subscription.EndDate,
                DateTime.UtcNow,
                SubscriptionRules.DefaultGracePeriodDays);

            if (!isEntitled)
            {
                throw new InvalidOperationException(
                    $"Subscription for tenant '{tenantId}' is in status '{subscription.Status}' and is not entitled to service.");
            }

            // 2. Locate active SubscriptionPlanParameter for parameterKey
            var planParameter = await _dbContext.SubscriptionLimits
                .Where(l => l.SubscriptionPlanId == subscription.SubscriptionPlanId && l.IsActive)
                .SelectMany(l => l.Parameters)
                .Where(p => p.IsActive && p.SubscriptionParameter.IsActive &&
                            p.SubscriptionParameter.ParameterKey.ToUpper() == parameterKey.Trim().ToUpper())
                .Include(p => p.SubscriptionParameter)
                .FirstOrDefaultAsync(cancellationToken);

            if (planParameter == null)
            {
                throw new InvalidOperationException(
                    $"Subscription limit configuration for parameter '{parameterKey}' is missing or inactive for plan '{subscription.SubscriptionPlanId}'.");
            }

            // 3. Billing period boundaries
            DateTime periodStart = subscription.CurrentPeriodStart != default ? subscription.CurrentPeriodStart : subscription.StartDate;
            DateTime periodEnd = subscription.CurrentPeriodEnd != default ? subscription.CurrentPeriodEnd : (subscription.EndDate ?? DateTime.MaxValue);

            // 4. Retrieve or initialize usage row for current period
            var usage = await _dbContext.SubscriptionUsages
                .FirstOrDefaultAsync(u => u.SubscriptionId == subscription.Id &&
                                         u.SubscriptionPlanParameterId == planParameter.Id &&
                                         u.PeriodStart == periodStart,
                                     cancellationToken);

            if (usage == null)
            {
                usage = new SubscriptionUsage
                {
                    Id = Guid.NewGuid(),
                    SubscriptionId = subscription.Id,
                    SubscriptionPlanParameterId = planParameter.Id,
                    UsageValue = 0,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    LastUpdated = DateTime.UtcNow
                };
                await _dbContext.SubscriptionUsages.AddAsync(usage, cancellationToken);
            }

            // 5. Verify limit threshold
            if (!planParameter.IsUnlimited)
            {
                decimal limitVal = planParameter.LimitValue ?? 0;
                if (usage.UsageValue + increment > limitVal)
                {
                    throw new InvalidOperationException(
                        $"Subscription limit exceeded for '{parameterKey}'. Limit: {limitVal}, Current usage: {usage.UsageValue}, Requested increment: {increment}.");
                }
            }

            // Update usage in SaaSDbContext
            usage.UsageValue += increment;
            usage.LastUpdated = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 6. Execute business entity creation
            T result;
            try
            {
                result = await createEntityAction();
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                throw;
            }

            // 7. Commit usage reservation
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch
                {
                    // Suppress secondary rollback exception to preserve primary exception
                }
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}