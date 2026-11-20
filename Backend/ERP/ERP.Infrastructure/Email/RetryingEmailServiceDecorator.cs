using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using ERP.Infrastructure.Email.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Email;

/// <summary>
/// Centralised decorator that adds automatic, configurable retry resilience around any IEmailService implementation.
/// Implements Tier 1 of the ApexERP Two-Tier Retry Hierarchy:
/// 1 initial attempt + up to MaxRetryAttempts automatic retries (maximum 6 attempts per dispatch cycle by default).
/// Strictly retries transient network and server faults, while failing immediately on non-retryable categories.
/// </summary>
public class RetryingEmailServiceDecorator : IEmailService
{
    private readonly IEmailService _inner;
    private readonly IOptions<EmailRetrySettings> _options;
    private readonly ILogger<RetryingEmailServiceDecorator> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RetryingEmailServiceDecorator(
        IEmailService inner,
        IOptions<EmailRetrySettings> options,
        ILogger<RetryingEmailServiceDecorator> logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _delay = delay ?? ((timeSpan, ct) => Task.Delay(timeSpan, ct));
    }

    public async Task<EmailResult> SendEmailAsync(
        EmailMessage message, 
        CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var settings = _options.Value;
        settings.Validate();

        var maxAttempts = 1 + settings.MaxRetryAttempts;
        EmailResult lastResult = EmailResult.Failure("Email delivery was not attempted.", EmailFailureCategory.TransientServer);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Email delivery was canceled before attempt {Attempt}/{MaxAttempts} for subject '{Subject}'.",
                    attempt, maxAttempts, message.Subject);
                return EmailResult.Failure("Email delivery was canceled.", EmailFailureCategory.Canceled);
            }

            _logger.LogInformation("Attempting email delivery (Attempt {Attempt}/{MaxAttempts}) for subject '{Subject}' to {RecipientCount} recipient(s)...",
                attempt, maxAttempts, message.Subject, message.To.Count);

            try
            {
                lastResult = await _inner.SendEmailAsync(message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Email delivery was canceled during transport execution on attempt {Attempt}/{MaxAttempts} for subject '{Subject}'.",
                    attempt, maxAttempts, message.Subject);
                return EmailResult.Failure("Email delivery was canceled during transport.", EmailFailureCategory.Canceled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected unhandled exception from inner email service on attempt {Attempt}/{MaxAttempts} for subject '{Subject}'.",
                    attempt, maxAttempts, message.Subject);
                lastResult = EmailResult.Failure($"Unexpected email service failure: {ex.Message}", EmailFailureCategory.TransientServer);
            }

            if (lastResult.IsSuccess)
            {
                if (attempt > 1)
                {
                    _logger.LogInformation("Email '{Subject}' dispatched successfully on retry attempt {Attempt}/{MaxAttempts}.",
                        message.Subject, attempt, maxAttempts);
                }
                return lastResult;
            }

            // Check if failure is retryable
            if (!IsRetryable(lastResult.FailureCategory))
            {
                _logger.LogWarning(
                    "Email attempt {Attempt}/{MaxAttempts} failed with non-retryable category '{Category}'. Halting automatic retry cycle. Error: {ErrorMessage}",
                    attempt, maxAttempts, lastResult.FailureCategory, lastResult.ErrorMessage);
                return lastResult;
            }

            // If we have exhausted all attempts, stop
            if (attempt >= maxAttempts)
            {
                _logger.LogError(
                    "Email delivery failed after exhausting all {MaxAttempts} attempts for subject '{Subject}'. Category: '{Category}', Error: {ErrorMessage}",
                    maxAttempts, message.Subject, lastResult.FailureCategory, lastResult.ErrorMessage);
                return lastResult;
            }

            // Calculate delay for next retry
            var delayMs = CalculateDelayMilliseconds(settings, attempt);

            _logger.LogInformation(
                "Email attempt {Attempt}/{MaxAttempts} failed with transient error ('{Category}'). Waiting {DelayMs}ms before retry...",
                attempt, maxAttempts, lastResult.FailureCategory, delayMs);

            try
            {
                await _delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Email retry delay was canceled after attempt {Attempt}/{MaxAttempts} for subject '{Subject}'.",
                    attempt, maxAttempts, message.Subject);
                return EmailResult.Failure("Email delivery was canceled during retry delay.", EmailFailureCategory.Canceled);
            }
        }

        return lastResult;
    }

    /// <summary>
    /// Determines whether a failure category is eligible for automatic Tier 1 retry.
    /// Only TransientNetwork and TransientServer are retryable.
    /// </summary>
    public static bool IsRetryable(EmailFailureCategory category) =>
        category is EmailFailureCategory.TransientNetwork or EmailFailureCategory.TransientServer;

    /// <summary>
    /// Computes delay for the specified retry index based on configured settings.
    /// Supports constant delay and optional exponential backoff capped at MaxRetryDelayMilliseconds.
    /// </summary>
    public static int CalculateDelayMilliseconds(EmailRetrySettings settings, int completedAttempt)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (!settings.UseExponentialBackoff)
            return settings.RetryDelayMilliseconds;

        // Exponential backoff: baseDelay * 2^(completedAttempt - 1), capped at MaxRetryDelayMilliseconds
        var factor = Math.Pow(2, completedAttempt - 1);
        var calculated = settings.RetryDelayMilliseconds * factor;
        return (int)Math.Min(settings.MaxRetryDelayMilliseconds, calculated);
    }
}
