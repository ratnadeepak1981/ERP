using System;

namespace ERP.Infrastructure.Email.Configuration;

/// <summary>
/// Strongly typed configuration options for email retry operations.
/// </summary>
public sealed class EmailRetrySettings
{
    public const string SectionName = "EmailRetrySettings";

    public int MaxRetryAttempts { get; set; } = 5;
    public int RetryDelayMilliseconds { get; set; } = 2000;
    public bool UseExponentialBackoff { get; set; } = false;
    public int MaxRetryDelayMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Validates retry settings parameters.
    /// Enforces:
    /// - MaxRetryAttempts in [0, 10]
    /// - RetryDelayMilliseconds >= 200 ms
    /// - MaxRetryDelayMilliseconds >= RetryDelayMilliseconds
    /// </summary>
    public void Validate()
    {
        if (MaxRetryAttempts < 0 || MaxRetryAttempts > 10)
            throw new InvalidOperationException($"MaxRetryAttempts '{MaxRetryAttempts}' is invalid. It must be between 0 and 10.");

        if (RetryDelayMilliseconds < 200)
            throw new InvalidOperationException($"RetryDelayMilliseconds '{RetryDelayMilliseconds}' is invalid. It must be at least 200 ms.");

        if (MaxRetryDelayMilliseconds < RetryDelayMilliseconds)
            throw new InvalidOperationException($"MaxRetryDelayMilliseconds '{MaxRetryDelayMilliseconds}' must be greater than or equal to RetryDelayMilliseconds '{RetryDelayMilliseconds}'.");
    }
}
