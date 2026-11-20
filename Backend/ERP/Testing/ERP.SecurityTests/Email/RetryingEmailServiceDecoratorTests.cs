using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using ERP.Infrastructure.Email;
using ERP.Infrastructure.Email.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.SecurityTests.Email;

public class RetryingEmailServiceDecoratorTests
{
    private class MockEmailService : IEmailService
    {
        private readonly Queue<EmailResult> _results = new();
        public int InvocationCount { get; private set; }
        public List<EmailMessage> RecordedMessages { get; } = new();
        public Func<EmailMessage, CancellationToken, Task<EmailResult>>? CustomHandler { get; set; }

        public void EnqueueResult(EmailResult result) => _results.Enqueue(result);

        public Task<EmailResult> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            RecordedMessages.Add(message);

            if (CustomHandler != null)
                return CustomHandler(message, cancellationToken);

            if (_results.Count > 0)
                return Task.FromResult(_results.Dequeue());

            return Task.FromResult(EmailResult.Success("default-success-id"));
        }
    }

    private static EmailMessage CreateValidMessage()
    {
        var msg = new EmailMessage
        {
            Subject = "Test Subject",
            HtmlBody = "<p>Test Content</p>",
            PlainTextBody = "Test Content"
        };
        msg.To.Add(new EmailRecipient("recipient@example.com"));
        return msg;
    }

    [Fact]
    public async Task SendEmailAsync_FirstAttemptSucceeds_ExecutesExactlyOnceAndReturnsSuccess()
    {
        // Scenario 1: First attempt succeeds -> 1 attempt made, success returned
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Success("msg-first-try"));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 2000,
            UseExponentialBackoff = false
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Equal("msg-first-try", result.MessageId);
        Assert.Equal(EmailFailureCategory.None, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_FirstAttemptFailsTransiently_SecondAttemptSucceeds_ExecutesTwiceAndReturnsSuccess()
    {
        // Scenario 2: First attempt fails with transient failure, second attempt succeeds -> 2 attempts made, success returned
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Connection timed out", EmailFailureCategory.TransientNetwork));
        mockInner.EnqueueResult(EmailResult.Success("msg-second-try"));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 2000,
            UseExponentialBackoff = false
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Equal("msg-second-try", result.MessageId);
        Assert.Equal(EmailFailureCategory.None, result.FailureCategory);
        Assert.Equal(2, mockInner.InvocationCount);
        Assert.Single(recordedDelays);
        Assert.Equal(TimeSpan.FromMilliseconds(2000), recordedDelays[0]);
    }

    [Fact]
    public async Task SendEmailAsync_AllAttemptsFailTransiently_ExecutesMaxSixAttemptsAndReturnsFailure()
    {
        // Scenario 3: All attempts fail transiently -> exactly 6 attempts made (1 initial + 5 retries), final failure returned
        var mockInner = new MockEmailService();
        for (var i = 1; i <= 6; i++)
        {
            mockInner.EnqueueResult(EmailResult.Failure($"SMTP 451 error on attempt {i}", EmailFailureCategory.TransientServer));
        }

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 2000,
            UseExponentialBackoff = false
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.TransientServer, result.FailureCategory);
        Assert.Contains("attempt 6", result.ErrorMessage);
        Assert.Equal(6, mockInner.InvocationCount);
        Assert.Equal(5, recordedDelays.Count); // 5 retries after initial attempt
    }

    [Fact]
    public async Task SendEmailAsync_MaxRetryAttemptsZero_ExecutesExactlyOneAttempt()
    {
        // Scenario 4: MaxRetryAttempts = 0 -> exactly 1 attempt made, failure returned, no retries
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Network down", EmailFailureCategory.TransientNetwork));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 0,
            RetryDelayMilliseconds = 2000
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.TransientNetwork, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentAuthenticationFailure_FailsImmediatelyWithoutRetry()
    {
        // Scenario 5: Permanent authentication failure -> 1 attempt, failure returned, no retries
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Invalid credentials", EmailFailureCategory.PermanentAuthentication));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentAuthentication, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentMailboxFailure_FailsImmediatelyWithoutRetry()
    {
        // Scenario 6: Permanent recipient/mailbox failure -> 1 attempt, failure returned, no retries
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("550 Mailbox not found", EmailFailureCategory.PermanentMailbox));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentMailbox, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentSecurityFailure_FailsImmediatelyWithoutRetry()
    {
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("TLS handshake failed", EmailFailureCategory.PermanentSecurity));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentSecurity, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_PermanentValidationFailure_FailsImmediatelyWithoutRetry()
    {
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Malformed recipient address", EmailFailureCategory.PermanentValidation));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentValidation, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_AmbiguousProtocolFailure_FailsImmediatelyWithoutAutomaticRetry()
    {
        // Scenario 9: Ambiguous outcome -> exactly 1 attempt made, failure returned, no automatic retry
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Connection reset after DATA before 250 OK", EmailFailureCategory.Ambiguous));

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.Ambiguous, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_CancellationDuringRetryDelay_AbortsImmediately()
    {
        // Scenario 7: Cancellation requested during retry delay -> aborts immediately, Canceled failure returned
        var mockInner = new MockEmailService();
        mockInner.EnqueueResult(EmailResult.Failure("Temporary network outage", EmailFailureCategory.TransientNetwork));

        using var cts = new CancellationTokenSource();

        var settings = new EmailRetrySettings { MaxRetryAttempts = 5, RetryDelayMilliseconds = 2000 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                // Simulate cancellation during delay wait
                cts.Cancel();
                throw new OperationCanceledException(ct);
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.Canceled, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
    }

    [Fact]
    public async Task SendEmailAsync_CancellationDuringTransportExecution_AbortsImmediately()
    {
        // Scenario 8: Cancellation requested during transport execution -> aborts immediately, Canceled failure returned
        using var cts = new CancellationTokenSource();
        var mockInner = new MockEmailService
        {
            CustomHandler = (msg, ct) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(ct);
            }
        };

        var recordedDelays = new List<TimeSpan>();
        var settings = new EmailRetrySettings { MaxRetryAttempts = 5 };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add(ts);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.Canceled, result.FailureCategory);
        Assert.Equal(1, mockInner.InvocationCount);
        Assert.Empty(recordedDelays);
    }

    [Fact]
    public async Task SendEmailAsync_ExponentialBackoffEnabled_CalculatesCorrectDelaysAndCapsAtMaxDelay()
    {
        // Scenario 11: Exponential backoff enabled -> verify delays follow exponential curve and cap at MaxRetryDelayMilliseconds
        var mockInner = new MockEmailService();
        for (var i = 1; i <= 6; i++)
        {
            mockInner.EnqueueResult(EmailResult.Failure("Network blip", EmailFailureCategory.TransientNetwork));
        }

        var recordedDelays = new List<int>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 2000,
            UseExponentialBackoff = true,
            MaxRetryDelayMilliseconds = 30000
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add((int)ts.TotalMilliseconds);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(6, mockInner.InvocationCount);
        Assert.Equal(5, recordedDelays.Count);

        // Verification of exponential progression:
        // Retry 1: 2000 * 2^0 = 2000
        // Retry 2: 2000 * 2^1 = 4000
        // Retry 3: 2000 * 2^2 = 8000
        // Retry 4: 2000 * 2^3 = 16000
        // Retry 5: 2000 * 2^4 = 32000 -> capped at 30000 ms
        Assert.Equal(2000, recordedDelays[0]);
        Assert.Equal(4000, recordedDelays[1]);
        Assert.Equal(8000, recordedDelays[2]);
        Assert.Equal(16000, recordedDelays[3]);
        Assert.Equal(30000, recordedDelays[4]); // Capped at MaxRetryDelayMilliseconds
    }

    [Fact]
    public async Task SendEmailAsync_ConstantDelay_UsesConstantDelayAcrossAllRetries()
    {
        var mockInner = new MockEmailService();
        for (var i = 1; i <= 6; i++)
        {
            mockInner.EnqueueResult(EmailResult.Failure("Network blip", EmailFailureCategory.TransientNetwork));
        }

        var recordedDelays = new List<int>();
        var settings = new EmailRetrySettings
        {
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 2000,
            UseExponentialBackoff = false,
            MaxRetryDelayMilliseconds = 30000
        };

        var decorator = new RetryingEmailServiceDecorator(
            mockInner,
            Options.Create(settings),
            NullLogger<RetryingEmailServiceDecorator>.Instance,
            (ts, ct) =>
            {
                recordedDelays.Add((int)ts.TotalMilliseconds);
                return Task.CompletedTask;
            });

        var message = CreateValidMessage();
        var result = await decorator.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(5, recordedDelays.Count);
        foreach (var delay in recordedDelays)
        {
            Assert.Equal(2000, delay);
        }
    }

    [Fact]
    public void EmailRetrySettings_Validation_EnforcesValidBoundaries()
    {
        // Scenario 12: Invalid retry settings -> startup validation fails
        var valid = new EmailRetrySettings();
        valid.Validate(); // Does not throw

        var negativeAttempts = new EmailRetrySettings { MaxRetryAttempts = -1 };
        Assert.Throws<InvalidOperationException>(() => negativeAttempts.Validate());

        var excessiveAttempts = new EmailRetrySettings { MaxRetryAttempts = 11 };
        Assert.Throws<InvalidOperationException>(() => excessiveAttempts.Validate());

        var lowDelay = new EmailRetrySettings { RetryDelayMilliseconds = 150 };
        Assert.Throws<InvalidOperationException>(() => lowDelay.Validate());

        var maxDelayTooLow = new EmailRetrySettings
        {
            RetryDelayMilliseconds = 5000,
            MaxRetryDelayMilliseconds = 2000
        };
        Assert.Throws<InvalidOperationException>(() => maxDelayTooLow.Validate());
    }

    [Fact]
    public void DependencyInjection_Registration_ResolvesRetryingDecoratorWrappingConcreteService()
    {
        // Integration test verifying DI composition root registration
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["SmtpSettings:Host"] = "smtp.example.com",
            ["SmtpSettings:Port"] = "587",
            ["SmtpSettings:EnableSsl"] = "true",
            ["SmtpSettings:SenderEmail"] = "test@example.com",
            ["SmtpSettings:SenderDisplayName"] = "Test System",
            ["SmtpSettings:Username"] = "testuser",
            ["SmtpSettings:Password"] = "testpass",
            ["EmailRetrySettings:MaxRetryAttempts"] = "3",
            ["EmailRetrySettings:RetryDelayMilliseconds"] = "1000",
            ["EmailRetrySettings:UseExponentialBackoff"] = "false",
            ["EmailRetrySettings:MaxRetryDelayMilliseconds"] = "10000"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        // Options registration
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));

        services.AddOptions<EmailRetrySettings>()
            .Bind(configuration.GetSection(EmailRetrySettings.SectionName))
            .Validate(settings =>
            {
                settings.Validate();
                return true;
            }, "Invalid EmailRetrySettings configuration.")
            .ValidateOnStart();

        services.AddSingleton<ISmtpClientFactory, DefaultSmtpClientFactory>();
        services.AddScoped<MailKitSmtpEmailService>();

        services.AddScoped<IEmailService>(sp =>
        {
            var transport = sp.GetRequiredService<MailKitSmtpEmailService>();
            var retrySettings = sp.GetRequiredService<IOptions<EmailRetrySettings>>();
            var logger = sp.GetRequiredService<ILogger<RetryingEmailServiceDecorator>>();
            return new RetryingEmailServiceDecorator(transport, retrySettings, logger);
        });

        using var provider = services.BuildServiceProvider();

        // Resolve concrete transport
        var concrete = provider.GetService<MailKitSmtpEmailService>();
        Assert.NotNull(concrete);

        // Resolve IEmailService and verify it is decorated
        var resolvedService = provider.GetService<IEmailService>();
        Assert.NotNull(resolvedService);
        Assert.IsType<RetryingEmailServiceDecorator>(resolvedService);

        // Verify bound settings
        var boundSettings = provider.GetRequiredService<IOptions<EmailRetrySettings>>().Value;
        Assert.Equal(3, boundSettings.MaxRetryAttempts);
        Assert.Equal(1000, boundSettings.RetryDelayMilliseconds);
        Assert.False(boundSettings.UseExponentialBackoff);
        Assert.Equal(10000, boundSettings.MaxRetryDelayMilliseconds);
    }
}
