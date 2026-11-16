using System;

namespace ERP.Application.Common.Email;

/// <summary>
/// Represents a binary file attachment to be included in an outgoing email.
/// </summary>
public sealed class EmailAttachment
{
    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Content { get; }

    public EmailAttachment(string fileName, string contentType, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Attachment file name cannot be empty.", nameof(fileName));

        FileName = fileName.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }
}
