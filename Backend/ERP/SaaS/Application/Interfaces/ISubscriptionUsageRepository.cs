using SaaS.Core.Models;

namespace SaaS.Application.Interfaces.Repositories;

public interface ISubscriptionUsageRepository
{
    Task AddAsync(SubscriptionUsage usage);

    Task SaveChangesAsync();

    Task<IReadOnlyList<SubscriptionPlanParameter>>
    GetActivePlanParametersAsync(Guid subscriptionPlanId);

}