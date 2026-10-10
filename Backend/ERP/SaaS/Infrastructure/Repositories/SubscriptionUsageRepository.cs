using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class SubscriptionUsageRepository
    : ISubscriptionUsageRepository
{
    private readonly SaaSDbContext _context;

    public SaaSDbContext Context => _context;

    public SubscriptionUsageRepository(SaaSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SubscriptionUsage usage)
    {
        await _context.SubscriptionUsages.AddAsync(usage);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SubscriptionPlanParameter>>
    GetActivePlanParametersAsync(Guid subscriptionPlanId)
    {
        return await _context.SubscriptionLimits
            .Where(x =>
                x.SubscriptionPlanId == subscriptionPlanId &&
                x.IsActive)
            .SelectMany(x => x.Parameters)
            .Where(x => x.IsActive)
            .ToListAsync();
    }
}