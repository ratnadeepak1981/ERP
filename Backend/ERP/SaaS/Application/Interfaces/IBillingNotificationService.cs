namespace SaaS.Application.Interfaces;

public interface IBillingNotificationService
{
    Task SendInvoiceDueReminderAsync(
        Guid tenantId,
        Guid invoiceId,
        string invoiceNumber,
        decimal amount,
        DateTime dueDateUtc,
        CancellationToken cancellationToken = default);

    Task SendPaymentFailureAlertAsync(
        Guid tenantId,
        Guid invoiceId,
        string invoiceNumber,
        decimal amount,
        string failureReason,
        CancellationToken cancellationToken = default);

    Task SendGracePeriodWarningAsync(
        Guid tenantId,
        Guid subscriptionId,
        DateTime gracePeriodEndUtc,
        CancellationToken cancellationToken = default);
}
