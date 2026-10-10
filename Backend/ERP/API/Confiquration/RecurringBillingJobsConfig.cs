using Hangfire;
using SaaS.Application.Interfaces;

namespace API.Configuration;

public static class RecurringBillingJobsConfig
{
    public static void ScheduleRecurringJobs(IRecurringJobManager recurringJobManager)
    {
        // 1. Due Invoices Processing: Daily at 06:00 UTC
        recurringJobManager.AddOrUpdate<IRecurringBillingService>(
            "billing-due-invoices-processing",
            service => service.ProcessDueInvoicesAsync(DateTime.UtcNow, CancellationToken.None),
            "0 6 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // 2. Grace Period Entitlements Evaluation: Daily at 08:00 UTC (Manual suspension rule preserved)
        recurringJobManager.AddOrUpdate<IRecurringBillingService>(
            "billing-grace-period-evaluation",
            service => service.EvaluateGracePeriodEntitlementsAsync(DateTime.UtcNow, CancellationToken.None),
            "0 8 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // 3. Billing Notifications & Reminders: Daily at 10:00 UTC
        recurringJobManager.AddOrUpdate<IRecurringBillingService>(
            "billing-notification-reminders",
            service => service.SendBillingNotificationsAsync(DateTime.UtcNow, CancellationToken.None),
            "0 10 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
