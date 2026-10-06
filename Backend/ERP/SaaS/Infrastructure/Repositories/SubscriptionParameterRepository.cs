using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Repositories;

public class SubscriptionParameterRepository
    : ISubscriptionParameterRepository
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionParameterRepository(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SubscriptionParameter? GetById(Guid id)
    {
        return _dbContext.SubscriptionParameters
            .FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<SubscriptionParameter> GetAll()
    {
        return _dbContext.SubscriptionParameters
            .OrderBy(x => x.ParameterKey)
            .ToList();
    }

    public bool ExistsByKey(string parameterKey)
    {
        return _dbContext.SubscriptionParameters
            .Any(x => x.ParameterKey == parameterKey);
    }

    public bool ExistsByKeyExceptId(
        string parameterKey,
        Guid id)
    {
        return _dbContext.SubscriptionParameters
            .Any(x =>
                x.Id != id &&
                x.ParameterKey == parameterKey);
    }

    public void Add(SubscriptionParameter parameter)
    {
        _dbContext.SubscriptionParameters.Add(parameter);
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}