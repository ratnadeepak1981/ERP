using SaaS.Application.DTOs;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionLimitService
{
    SubscriptionLimit Create(
        CreateSubscriptionLimitRequest request);

    SubscriptionLimit? GetById(Guid id);

    IReadOnlyList<SubscriptionLimit> GetAll();

    SubscriptionLimit Update(
        Guid id,
        UpdateSubscriptionLimitRequest request);
}