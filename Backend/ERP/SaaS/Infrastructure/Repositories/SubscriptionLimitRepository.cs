using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Repositories;

public class SubscriptionLimitRepository
    : ISubscriptionLimitRepository
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionLimitRepository(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SubscriptionLimit? GetById(Guid id)
    {
        return _dbContext.SubscriptionLimits
            .FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<SubscriptionLimit> GetAll()
    {
        return _dbContext.SubscriptionLimits
            .OrderBy(x => x.SubscriptionPlanId)
            .ToList();
    }

    public bool ExistsByPlanId(Guid subscriptionPlanId)
    {
        return _dbContext.SubscriptionLimits
            .Any(x => x.SubscriptionPlanId == subscriptionPlanId);
    }

    public bool ExistsByPlanIdExceptId(
        Guid subscriptionPlanId,
        Guid id)
    {
        return _dbContext.SubscriptionLimits
            .Any(x =>
                x.Id != id &&
                x.SubscriptionPlanId == subscriptionPlanId);
    }

    public void Add(SubscriptionLimit limit)
    {
        _dbContext.SubscriptionLimits.Add(limit);
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}