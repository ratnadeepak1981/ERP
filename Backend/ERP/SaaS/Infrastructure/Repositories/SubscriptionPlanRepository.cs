using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Repositories;

public class SubscriptionPlanRepository
    : ISubscriptionPlanRepository
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionPlanRepository(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SubscriptionPlan? GetById(Guid id)
    {
        return _dbContext.SubscriptionPlans
            .FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<SubscriptionPlan> GetAll()
    {
        return _dbContext.SubscriptionPlans
            .OrderBy(x => x.Name)
            .ToList();
    }

    public bool ExistsByCode(string code)
    {
        return _dbContext.SubscriptionPlans
            .Any(x => x.Code == code);
    }

    public bool ExistsByCodeExceptId(
        string code,
        Guid id)
    {
        return _dbContext.SubscriptionPlans
            .Any(x =>
                x.Id != id &&
                x.Code == code);
    }

    public void Add(SubscriptionPlan plan)
    {
        _dbContext.SubscriptionPlans.Add(plan);
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}