using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task AddAsync(
        Subscription subscription,
        CancellationToken cancellationToken = default);

    Task<Subscription?> GetActiveByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}