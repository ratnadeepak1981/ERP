using SaaS.Application.DTOs;
using SaaS.Application.Interfaces;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;

namespace SaaS.Application.Services;

public class SubscriptionParameterService
    : ISubscriptionParameterService
{
    private readonly ISubscriptionParameterRepository _repository;

    public SubscriptionParameterService(
        ISubscriptionParameterRepository repository)
    {
        _repository = repository;
    }

    public SubscriptionParameter Create(
        CreateSubscriptionParameterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ParameterKey))
        {
            throw new ArgumentException(
                "Parameter key is required.",
                nameof(request.ParameterKey));
        }

        string key =
            request.ParameterKey.Trim().ToUpperInvariant();

        if (_repository.ExistsByKey(key))
        {
            throw new InvalidOperationException(
                "This parameter already exists.");
        }

        SubscriptionParameter parameter =
            new SubscriptionParameter
            {
                Id = Guid.NewGuid(),
                ParameterKey = key,
                ParameterType = request.ParameterType,
                IsActive = request.IsActive
            };

        _repository.Add(parameter);
        _repository.SaveChanges();

        return parameter;
    }

    public SubscriptionParameter? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public IReadOnlyList<SubscriptionParameter> GetAll()
    {
        return _repository.GetAll();
    }

    public SubscriptionParameter Update(
        Guid id,
        UpdateSubscriptionParameterRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ParameterKey))
        {
            throw new ArgumentException(
                "Parameter key is required.",
                nameof(request.ParameterKey));
        }

        SubscriptionParameter? parameter =
            _repository.GetById(id);

        if (parameter == null)
        {
            throw new KeyNotFoundException(
                "Subscription parameter was not found.");
        }

        string key =
            request.ParameterKey.Trim().ToUpperInvariant();

        if (_repository.ExistsByKeyExceptId(key, id))
        {
            throw new InvalidOperationException(
                "This parameter already exists.");
        }

        parameter.ParameterKey = key;
        parameter.ParameterType = request.ParameterType;
        parameter.IsActive = request.IsActive;

        _repository.SaveChanges();

        return parameter;
    }
}