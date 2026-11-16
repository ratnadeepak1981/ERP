using System;
using System.Collections.Generic;

namespace ERP.Application.Common.Email;

/// <summary>
/// Encapsulates a complete email message payload, independent of transport or presentation details.
/// </summary>
public sealed class EmailMessage
{
    public List<EmailRecipient> To { get; } = new();
    public List<EmailRecipient> Cc { get; } = new();
    public List<EmailRecipient> Bcc { get; } = new();
    public EmailRecipient? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public List<EmailAttachment> Attachments { get; } = new();
    public Dictionary<string, string> Headers { get; } = new();

    /// <summary>
    /// Validates the message invariant rules before dispatch.
    /// </summary>
    public void Validate()
    {
        if (To.Count == 0)
            throw new InvalidOperationException("At least one recipient (To) must be specified.");

        if (string.IsNullOrWhiteSpace(Subject))
            throw new InvalidOperationException("Email subject cannot be empty.");

        if (string.IsNullOrWhiteSpace(HtmlBody) && string.IsNullOrWhiteSpace(PlainTextBody))
            throw new InvalidOperationException("Email message must have either an HTML body or a plain-text body.");
    }
}
