using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Repositories;

public class SubscriptionPlanParameterRepository
    : ISubscriptionPlanParameterRepository
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionPlanParameterRepository(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SubscriptionPlanParameter? GetById(Guid id)
    {
        return _dbContext.SubscriptionPlanParameters
            .FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<SubscriptionPlanParameter> GetAll()
    {
        return _dbContext.SubscriptionPlanParameters
            .OrderBy(x => x.SubscriptionLimitId)
            .ThenBy(x => x.SubscriptionParameterId)
            .ToList();
    }

    public bool Exists(
        Guid subscriptionLimitId,
        Guid subscriptionParameterId)
    {
        return _dbContext.SubscriptionPlanParameters
            .Any(x =>
                x.SubscriptionLimitId == subscriptionLimitId &&
                x.SubscriptionParameterId == subscriptionParameterId);
    }

    public bool ExistsExceptId(
        Guid subscriptionLimitId,
        Guid subscriptionParameterId,
        Guid id)
    {
        return _dbContext.SubscriptionPlanParameters
            .Any(x =>
                x.Id != id &&
                x.SubscriptionLimitId == subscriptionLimitId &&
                x.SubscriptionParameterId == subscriptionParameterId);
    }

    public void Add(SubscriptionPlanParameter parameter)
    {
        _dbContext.SubscriptionPlanParameters.Add(parameter);
    }

    public bool SubscriptionLimitExists(Guid id)
    {
        return _dbContext.SubscriptionLimits
            .Any(x => x.Id == id);
    }

    public bool SubscriptionParameterExists(Guid id)
    {
        return _dbContext.SubscriptionParameters
            .Any(x => x.Id == id);
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}