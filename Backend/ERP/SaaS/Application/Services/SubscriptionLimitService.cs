using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class SubscriptionLimitService
    : ISubscriptionLimitService
{
    private readonly ISubscriptionLimitRepository _repository;

    public SubscriptionLimitService(
        ISubscriptionLimitRepository repository)
    {
        _repository = repository;
    }

    public SubscriptionLimit Create(
        CreateSubscriptionLimitRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.SubscriptionPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Subscription plan is required.",
                nameof(request.SubscriptionPlanId));
        }

        if (_repository.ExistsByPlanId(
            request.SubscriptionPlanId))
        {
            throw new InvalidOperationException(
                "A subscription limit already exists for this plan.");
        }

        SubscriptionLimit limit =
            new SubscriptionLimit
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId =
                    request.SubscriptionPlanId,
                IsActive = request.IsActive
            };

        _repository.Add(limit);
        _repository.SaveChanges();

        return limit;
    }

    public SubscriptionLimit? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public IReadOnlyList<SubscriptionLimit> GetAll()
    {
        return _repository.GetAll();
    }

    public SubscriptionLimit Update(
        Guid id,
        UpdateSubscriptionLimitRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        SubscriptionLimit? limit =
            _repository.GetById(id);

        if (limit == null)
        {
            throw new KeyNotFoundException(
                "Subscription limit was not found.");
        }

        limit.IsActive = request.IsActive;

        _repository.SaveChanges();

        return limit;
    }
}