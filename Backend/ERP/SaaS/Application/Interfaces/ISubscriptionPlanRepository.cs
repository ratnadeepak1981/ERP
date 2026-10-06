using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ISubscriptionPlanRepository
{
    SubscriptionPlan? GetById(Guid id);

    IReadOnlyList<SubscriptionPlan> GetAll();

    bool ExistsByCode(string code);

    bool ExistsByCodeExceptId(
        string code,
        Guid id);

    void Add(SubscriptionPlan plan);

    void SaveChanges();
}