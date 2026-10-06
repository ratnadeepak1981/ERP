using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionParameterService
{
    SubscriptionParameter Create(
        CreateSubscriptionParameterRequest request);

    SubscriptionParameter? GetById(Guid id);

    IReadOnlyList<SubscriptionParameter> GetAll();

    SubscriptionParameter Update(
        Guid id,
        UpdateSubscriptionParameterRequest request);
}