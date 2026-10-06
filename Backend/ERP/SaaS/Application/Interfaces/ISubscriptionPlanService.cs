using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionPlanService
{
    SubscriptionPlan Create(
        CreateSubscriptionPlanRequest request);

    SubscriptionPlan? GetById(Guid id);

    IReadOnlyList<SubscriptionPlan> GetAll();

    SubscriptionPlan Update(
        Guid id,
        UpdateSubscriptionPlanRequest request);

    void Deactivate(Guid id);
}