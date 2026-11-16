using System;

namespace ERP.Infrastructure.Email.Configuration;

/// <summary>
/// Strongly typed configuration options for SMTP transport.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = "ApexERP";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 15000;

    /// <summary>
    /// Validates required configuration parameters before application start.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("SMTP Host is required and cannot be empty.");

        if (Port <= 0 || Port > 65535)
            throw new InvalidOperationException($"SMTP Port '{Port}' is invalid. It must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(SenderEmail))
            throw new InvalidOperationException("SMTP SenderEmail is required.");
    }
}
