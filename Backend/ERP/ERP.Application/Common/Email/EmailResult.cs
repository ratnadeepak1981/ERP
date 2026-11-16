using System;

namespace ERP.Application.Common.Email;

/// <summary>
/// Represents the outcome of an email transmission attempt.
/// </summary>
public sealed class EmailResult
{
    public bool IsSuccess { get; private init; }
    public string? MessageId { get; private init; }
    public string? ErrorMessage { get; private init; }
    public DateTime TimestampUtc { get; private init; }

    public static EmailResult Success(string? messageId = null) => new()
    {
        IsSuccess = true,
        MessageId = messageId,
        TimestampUtc = DateTime.UtcNow
    };

    public static EmailResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage ?? "Unknown email dispatch failure.",
        TimestampUtc = DateTime.UtcNow
    };
}
