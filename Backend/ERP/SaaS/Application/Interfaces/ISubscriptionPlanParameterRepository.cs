using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ISubscriptionPlanParameterRepository
{
    SubscriptionPlanParameter? GetById(Guid id);

    IReadOnlyList<SubscriptionPlanParameter> GetAll();

    bool Exists(
        Guid subscriptionLimitId,
        Guid subscriptionParameterId);

    bool ExistsExceptId(
        Guid subscriptionLimitId,
        Guid subscriptionParameterId,
        Guid id);

    void Add(SubscriptionPlanParameter parameter);

    bool SubscriptionLimitExists(Guid id);
    bool SubscriptionParameterExists(Guid id);
    void SaveChanges();
}