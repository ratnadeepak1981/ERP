using System;
using System.Threading;
using System.Threading.Tasks;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionUsageService
{
    Task InitializeUsageAsync(
        Subscription subscription);

    Task<T> ExecuteWithUsageLimitAsync<T>(
        Guid tenantId,
        string parameterKey,
        decimal increment,
        Func<Task<T>> createEntityAction,
        CancellationToken cancellationToken = default);
}