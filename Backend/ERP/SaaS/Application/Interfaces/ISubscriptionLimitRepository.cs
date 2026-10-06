using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ISubscriptionLimitRepository
{
    SubscriptionLimit? GetById(Guid id);

    IReadOnlyList<SubscriptionLimit> GetAll();

    bool ExistsByPlanId(Guid subscriptionPlanId);

    bool ExistsByPlanIdExceptId(
        Guid subscriptionPlanId,
        Guid id);

    void Add(SubscriptionLimit limit);

    void SaveChanges();
}