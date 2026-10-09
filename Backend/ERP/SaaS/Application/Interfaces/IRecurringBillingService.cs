using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface IRecurringBillingService
{
    Task<SubscriptionInvoice?> GenerateRecurringInvoiceAsync(
        Guid subscriptionId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task ProcessDueInvoicesAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task EvaluateGracePeriodEntitlementsAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task SendBillingNotificationsAsync(
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
