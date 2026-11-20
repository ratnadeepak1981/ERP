using System;

namespace ERP.Application.Common.Email;

/// <summary>
/// Represents the outcome of an email transmission attempt.
/// Includes structured failure classification for deterministic retry decisions.
/// </summary>
public sealed class EmailResult
{
    public bool IsSuccess { get; private init; }
    public string? MessageId { get; private init; }
    public string? ErrorMessage { get; private init; }
    public EmailFailureCategory FailureCategory { get; private init; } = EmailFailureCategory.None;
    public DateTime TimestampUtc { get; private init; }

    public static EmailResult Success(string? messageId = null) => new()
    {
        IsSuccess = true,
        MessageId = messageId,
        FailureCategory = EmailFailureCategory.None,
        TimestampUtc = DateTime.UtcNow
    };

    public static EmailResult Failure(
        string errorMessage, 
        EmailFailureCategory failureCategory = EmailFailureCategory.TransientServer) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage ?? "Unknown email dispatch failure.",
        FailureCategory = failureCategory,
        TimestampUtc = DateTime.UtcNow
    };
}
