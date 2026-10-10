using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SaaS.Application.Interfaces;

namespace SaaS.Application.Services;

public class DummyBillingNotificationService : IBillingNotificationService
{
    private readonly ILogger<DummyBillingNotificationService> _logger;

    // In-memory record for test assertion and idempotency auditing
    public static readonly ConcurrentBag<string> SentNotifications = new();

    public DummyBillingNotificationService(ILogger<DummyBillingNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendInvoiceDueReminderAsync(
        Guid tenantId,
        Guid invoiceId,
        string invoiceNumber,
        decimal amount,
        DateTime dueDateUtc,
        CancellationToken cancellationToken = default)
    {
        string record = $"DueReminder|{tenantId}|{invoiceId}|{invoiceNumber}|{amount:N2}|{dueDateUtc:yyyy-MM-dd}";
        SentNotifications.Add(record);
        _logger.LogInformation("Billing Notification: {Record}", record);
        return Task.CompletedTask;
    }

    public Task SendPaymentFailureAlertAsync(
        Guid tenantId,
        Guid invoiceId,
        string invoiceNumber,
        decimal amount,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        string record = $"PaymentFailure|{tenantId}|{invoiceId}|{invoiceNumber}|{amount:N2}|{failureReason}";
        SentNotifications.Add(record);
        _logger.LogWarning("Billing Alert: {Record}", record);
        return Task.CompletedTask;
    }

    public Task SendGracePeriodWarningAsync(
        Guid tenantId,
        Guid subscriptionId,
        DateTime gracePeriodEndUtc,
        CancellationToken cancellationToken = default)
    {
        string record = $"GraceWarning|{tenantId}|{subscriptionId}|{gracePeriodEndUtc:yyyy-MM-dd}";
        SentNotifications.Add(record);
        _logger.LogWarning("Grace Warning: {Record}", record);
        return Task.CompletedTask;
    }
}
