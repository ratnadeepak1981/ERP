using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ISubscriptionUsageService
{
    Task InitializeUsageAsync(
        Subscription subscription);
}