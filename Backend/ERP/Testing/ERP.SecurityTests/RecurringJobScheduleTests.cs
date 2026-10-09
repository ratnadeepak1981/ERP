using System;
using System.Collections.Generic;
using API.Configuration;
using Hangfire;
using Hangfire.Common;
using SaaS.Application.Interfaces;
using Xunit;

namespace ERP.SecurityTests;

[Trait("Category", "Unit")]
public class RecurringJobScheduleTests
{
    private class FakeRecurringJobManager : IRecurringJobManager
    {
        public class JobRegistration
        {
            public string RecurringJobId { get; set; } = string.Empty;
            public Job Job { get; set; } = null!;
            public string CronExpression { get; set; } = string.Empty;
            public RecurringJobOptions Options { get; set; } = new();
        }

        public readonly Dictionary<string, JobRegistration> Registrations = new(StringComparer.OrdinalIgnoreCase);

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
        {
            Registrations[recurringJobId] = new JobRegistration
            {
                RecurringJobId = recurringJobId,
                Job = job,
                CronExpression = cronExpression,
                Options = options
            };
        }

        public void RemoveIfExists(string recurringJobId)
        {
            Registrations.Remove(recurringJobId);
        }

        public void Trigger(string recurringJobId)
        {
            // No-op for unit testing
        }
    }

    [Fact]
    public void ScheduleRecurringJobs_RegistersAllThreeBillingJobsWithAccurateSchedulesAndUtcTimeZone()
    {
        // Arrange
        var fakeManager = new FakeRecurringJobManager();

        // Act
        RecurringBillingJobsConfig.ScheduleRecurringJobs(fakeManager);

        // Assert: Exactly 3 jobs registered
        Assert.Equal(3, fakeManager.Registrations.Count);

        // 1. Due Invoices Processing
        Assert.True(fakeManager.Registrations.ContainsKey("billing-due-invoices-processing"),
            "billing-due-invoices-processing job registration is missing");
        var dueInvoiceJob = fakeManager.Registrations["billing-due-invoices-processing"];
        Assert.Equal("0 6 * * *", dueInvoiceJob.CronExpression);
        Assert.Equal(TimeZoneInfo.Utc, dueInvoiceJob.Options.TimeZone);
        Assert.Equal(typeof(IRecurringBillingService), dueInvoiceJob.Job.Type);
        Assert.Equal(nameof(IRecurringBillingService.ProcessDueInvoicesAsync), dueInvoiceJob.Job.Method.Name);

        // 2. Grace Period Entitlements Evaluation
        Assert.True(fakeManager.Registrations.ContainsKey("billing-grace-period-evaluation"),
            "billing-grace-period-evaluation job registration is missing");
        var gracePeriodJob = fakeManager.Registrations["billing-grace-period-evaluation"];
        Assert.Equal("0 8 * * *", gracePeriodJob.CronExpression);
        Assert.Equal(TimeZoneInfo.Utc, gracePeriodJob.Options.TimeZone);
        Assert.Equal(typeof(IRecurringBillingService), gracePeriodJob.Job.Type);
        Assert.Equal(nameof(IRecurringBillingService.EvaluateGracePeriodEntitlementsAsync), gracePeriodJob.Job.Method.Name);

        // 3. Billing Notifications & Reminders
        Assert.True(fakeManager.Registrations.ContainsKey("billing-notification-reminders"),
            "billing-notification-reminders job registration is missing");
        var notificationJob = fakeManager.Registrations["billing-notification-reminders"];
        Assert.Equal("0 10 * * *", notificationJob.CronExpression);
        Assert.Equal(TimeZoneInfo.Utc, notificationJob.Options.TimeZone);
        Assert.Equal(typeof(IRecurringBillingService), notificationJob.Job.Type);
        Assert.Equal(nameof(IRecurringBillingService.SendBillingNotificationsAsync), notificationJob.Job.Method.Name);
    }
}
