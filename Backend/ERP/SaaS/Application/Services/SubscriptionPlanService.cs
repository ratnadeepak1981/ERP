using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Application.Services;

public class SubscriptionPlanService
    : ISubscriptionPlanService
{
    private readonly SaaSDbContext _dbContext;

    public SubscriptionPlanService(
        SaaSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SubscriptionPlan Create(
        CreateSubscriptionPlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException(
                "Plan name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException(
                "Plan code is required.");
        }

        if (request.Price < 0)
        {
            throw new ArgumentException(
                "Plan price cannot be negative.");
        }

        string code = request.Code.Trim().ToUpperInvariant();

        bool exists = _dbContext.SubscriptionPlans
            .Any(x => x.Code == code);

        if (exists)
        {
            throw new InvalidOperationException(
                "A subscription plan with this code already exists.");
        }

        SubscriptionPlan plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = code,
            Description = request.Description.Trim(),
            Price = request.Price,
            BillingCycle = request.BillingCycle,
            IsActive = request.IsActive
        };

        _dbContext.SubscriptionPlans.Add(plan);
        _dbContext.SaveChanges();

        return plan;
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

    public SubscriptionPlan Update(
        Guid id,
        UpdateSubscriptionPlanRequest request)
    {
        SubscriptionPlan? plan =
            _dbContext.SubscriptionPlans
                .FirstOrDefault(x => x.Id == id);

        if (plan == null)
        {
            throw new InvalidOperationException(
                "Subscription plan not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException(
                "Plan name is required.");
        }

        if (request.Price < 0)
        {
            throw new ArgumentException(
                "Plan price cannot be negative.");
        }

        plan.Name = request.Name.Trim();
        plan.Description = request.Description.Trim();
        plan.Price = request.Price;
        plan.BillingCycle = request.BillingCycle;
        plan.IsActive = request.IsActive;

        _dbContext.SaveChanges();

        return plan;
    }

    public void Deactivate(Guid id)
    {
        SubscriptionPlan? plan =
            _dbContext.SubscriptionPlans
                .FirstOrDefault(x => x.Id == id);

        if (plan == null)
        {
            throw new InvalidOperationException(
                "Subscription plan not found.");
        }

        plan.IsActive = false;

        _dbContext.SaveChanges();
    }
}