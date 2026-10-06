using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class SubscriptionPlanParameterService
    : ISubscriptionPlanParameterService
{
    private readonly ISubscriptionPlanParameterRepository _repository;

    public SubscriptionPlanParameterService(
        ISubscriptionPlanParameterRepository repository)
    {
        _repository = repository;
    }

    public SubscriptionPlanParameter Create(
        CreateSubscriptionPlanParameterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.SubscriptionLimitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Subscription limit is required.",
                nameof(request.SubscriptionLimitId));
        }

        if (request.SubscriptionParameterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Subscription parameter is required.",
                nameof(request.SubscriptionParameterId));
        }

        if (!_repository.SubscriptionLimitExists(
                request.SubscriptionLimitId))
        {
            throw new KeyNotFoundException(
                "Subscription limit was not found.");
        }

        if (!_repository.SubscriptionParameterExists(
                request.SubscriptionParameterId))
        {
            throw new KeyNotFoundException(
                "Subscription parameter was not found.");
        }

        if (!request.IsUnlimited &&
            request.LimitValue == null)
        {
            throw new ArgumentException(
                "Limit value is required when the parameter is not unlimited.",
                nameof(request.LimitValue));
        }

        if (request.IsUnlimited &&
            request.LimitValue != null)
        {
            throw new ArgumentException(
                "Limit value must be null when the parameter is unlimited.",
                nameof(request.LimitValue));
        }

        bool exists =
            _repository.Exists(
                request.SubscriptionLimitId,
                request.SubscriptionParameterId);

        if (exists)
        {
            throw new InvalidOperationException(
                "This parameter is already assigned to the subscription limit.");
        }

        SubscriptionPlanParameter parameter =
            new SubscriptionPlanParameter
            {
                Id = Guid.NewGuid(),
                SubscriptionLimitId =
                    request.SubscriptionLimitId,
                SubscriptionParameterId =
                    request.SubscriptionParameterId,
                LimitValue = request.LimitValue,
                IsUnlimited = request.IsUnlimited,
                Duration = request.Duration,
                IsActive = request.IsActive
            };

        _repository.Add(parameter);
        _repository.SaveChanges();

        return parameter;
    }

    public SubscriptionPlanParameter? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public IReadOnlyList<SubscriptionPlanParameter> GetAll()
    {
        return _repository.GetAll();
    }

    public SubscriptionPlanParameter Update(
        Guid id,
        UpdateSubscriptionPlanParameterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!request.IsUnlimited &&
            request.LimitValue == null)
        {
            throw new ArgumentException(
                "Limit value is required when the parameter is not unlimited.",
                nameof(request.LimitValue));
        }

        if (request.IsUnlimited &&
            request.LimitValue != null)
        {
            throw new ArgumentException(
                "Limit value must be null when the parameter is unlimited.",
                nameof(request.LimitValue));
        }

        SubscriptionPlanParameter? parameter =
            _repository.GetById(id);

        if (parameter == null)
        {
            throw new KeyNotFoundException(
                "Subscription plan parameter was not found.");
        }

        parameter.LimitValue = request.LimitValue;
        parameter.IsUnlimited = request.IsUnlimited;
        parameter.Duration = request.Duration;
        parameter.IsActive = request.IsActive;

        _repository.SaveChanges();

        return parameter;
    }
}