using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly SaaSDbContext _context;

    public SubscriptionRepository(SaaSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        Subscription subscription,
        CancellationToken cancellationToken = default)
    {
        await _context.Subscriptions.AddAsync(
            subscription,
            cancellationToken);
    }

    public async Task<Subscription?> GetActiveByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.IsActive,
                cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlanParameter>>
    GetActivePlanParametersAsync(
        Guid subscriptionPlanId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionLimits
            .Where(x =>
                x.SubscriptionPlanId == subscriptionPlanId &&
                x.IsActive)
            .SelectMany(x => x.Parameters)
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
    }
}