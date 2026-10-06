using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ISubscriptionParameterRepository
{
    SubscriptionParameter? GetById(Guid id);

    IReadOnlyList<SubscriptionParameter> GetAll();

    bool ExistsByKey(string parameterKey);

    bool ExistsByKeyExceptId(
        string parameterKey,
        Guid id);

    void Add(SubscriptionParameter parameter);

    void SaveChanges();
}