using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionPlanParameterService
{
    SubscriptionPlanParameter Create(
        CreateSubscriptionPlanParameterRequest request);

    SubscriptionPlanParameter? GetById(Guid id);

    IReadOnlyList<SubscriptionPlanParameter> GetAll();

    SubscriptionPlanParameter Update(
        Guid id,
        UpdateSubscriptionPlanParameterRequest request);
}