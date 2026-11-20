using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using ERP.Infrastructure.Email.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ERP.Infrastructure.Email;

/// <summary>
/// Centralised MailKit-based SMTP email service for ApexERP.
/// Handles connection, STARTTLS negotiation, authentication, MIME construction, and delivery.
/// </summary>
public class MailKitSmtpEmailService : IEmailService
{
    private readonly IOptions<SmtpOptions> _options;
    private readonly ILogger<MailKitSmtpEmailService> _logger;
    private readonly ISmtpClientFactory _smtpClientFactory;

    public MailKitSmtpEmailService(
        IOptions<SmtpOptions> options,
        ILogger<MailKitSmtpEmailService> logger,
        ISmtpClientFactory? smtpClientFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _smtpClientFactory = smtpClientFactory ?? new DefaultSmtpClientFactory();
    }

    public virtual async Task<EmailResult> SendEmailAsync(
        EmailMessage message, 
        CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        try
        {
            // Validate message invariants
            message.Validate();

            var config = _options.Value;
            config.Validate();

            var mimeMessage = BuildMimeMessage(message, config);

            var secureSocketOptions = config.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            using var client = _smtpClientFactory.CreateClient();

            _logger.LogInformation("Connecting to SMTP server {Host}:{Port} (STARTTLS: {EnableSsl})...",
                config.Host, config.Port, config.EnableSsl);

            await client.ConnectAsync(config.Host, config.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(config.Username))
            {
                _logger.LogInformation("Authenticating with SMTP server as user '{Username}'...", config.Username);
                await client.AuthenticateAsync(config.Username, config.Password, cancellationToken);
            }

            _logger.LogInformation("Sending email '{Subject}' to {RecipientCount} recipient(s)...",
                message.Subject, message.To.Count);

            var response = await client.SendAsync(mimeMessage, cancellationToken);

            try
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception disconnectEx)
            {
                _logger.LogWarning(disconnectEx, "SMTP disconnect failed after email was successfully transmitted for subject '{Subject}'. MessageId: {MessageId}",
                    message.Subject, mimeMessage.MessageId);
            }

            _logger.LogInformation("Email '{Subject}' dispatched successfully. MessageId: {MessageId}, Response: {Response}",
                message.Subject, mimeMessage.MessageId, response);

            return EmailResult.Success(mimeMessage.MessageId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Email delivery was canceled for subject '{Subject}'.", message.Subject);
            return EmailResult.Failure($"Email delivery was canceled: {ex.Message}", EmailFailureCategory.Canceled);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed for user '{Username}' on {Host}:{Port}.",
                _options.Value.Username, _options.Value.Host, _options.Value.Port);
            return EmailResult.Failure($"SMTP authentication failed: {ex.Message}", EmailFailureCategory.PermanentAuthentication);
        }
        catch (System.Security.Authentication.AuthenticationException ex)
        {
            _logger.LogError(ex, "TLS security handshake failed connecting to {Host}:{Port}.",
                _options.Value.Host, _options.Value.Port);
            return EmailResult.Failure($"TLS security handshake failed: {ex.Message}", EmailFailureCategory.PermanentSecurity);
        }
        catch (SmtpCommandException ex)
        {
            var category = (int)ex.StatusCode >= 400 && (int)ex.StatusCode < 500
                ? EmailFailureCategory.TransientServer
                : EmailFailureCategory.PermanentMailbox;

            _logger.LogError(ex, "SMTP command error during email transmission (StatusCode: {StatusCode}, ErrorCode: {ErrorCode}, Category: {Category}): {Message}",
                ex.StatusCode, ex.ErrorCode, category, ex.Message);
            return EmailResult.Failure($"SMTP error ({ex.StatusCode}): {ex.Message}", category);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol failure during email delivery for subject '{Subject}': {Message}",
                message.Subject, ex.Message);
            return EmailResult.Failure($"SMTP protocol failure: {ex.Message}", EmailFailureCategory.Ambiguous);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Network connection error while connecting to SMTP server {Host}:{Port}.",
                _options.Value.Host, _options.Value.Port);
            return EmailResult.Failure($"Network connection failure: {ex.Message}", EmailFailureCategory.TransientNetwork);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Network timeout while communicating with SMTP server {Host}:{Port}.",
                _options.Value.Host, _options.Value.Port);
            return EmailResult.Failure($"SMTP connection timed out: {ex.Message}", EmailFailureCategory.TransientNetwork);
        }
        catch (IOException ex) when (ex.InnerException is SocketException || ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Network I/O error while communicating with SMTP server {Host}:{Port}.",
                _options.Value.Host, _options.Value.Port);
            return EmailResult.Failure($"Network connection failure: {ex.Message}", EmailFailureCategory.TransientNetwork);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            _logger.LogError(ex, "Validation failure for email subject '{Subject}'.", message.Subject);
            return EmailResult.Failure($"Validation failed: {ex.Message}", EmailFailureCategory.PermanentValidation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during email delivery for subject '{Subject}'.", message.Subject);
            return EmailResult.Failure($"Failed to deliver email: {ex.Message}", EmailFailureCategory.TransientServer);
        }
    }

    private static MimeMessage BuildMimeMessage(EmailMessage message, SmtpOptions config)
    {
        var mimeMessage = new MimeMessage();

        // From
        var fromAddress = !string.IsNullOrWhiteSpace(config.SenderEmail) ? config.SenderEmail : config.Username;
        mimeMessage.From.Add(new MailboxAddress(config.SenderDisplayName, fromAddress));

        // To
        foreach (var recipient in message.To)
        {
            mimeMessage.To.Add(new MailboxAddress(recipient.DisplayName, recipient.Address));
        }

        // CC
        foreach (var cc in message.Cc)
        {
            mimeMessage.Cc.Add(new MailboxAddress(cc.DisplayName, cc.Address));
        }

        // BCC
        foreach (var bcc in message.Bcc)
        {
            mimeMessage.Bcc.Add(new MailboxAddress(bcc.DisplayName, bcc.Address));
        }

        // Reply-To
        if (message.ReplyTo != null)
        {
            mimeMessage.ReplyTo.Add(new MailboxAddress(message.ReplyTo.DisplayName, message.ReplyTo.Address));
        }

        mimeMessage.Subject = message.Subject;

        // Custom headers
        foreach (var (key, value) in message.Headers)
        {
            mimeMessage.Headers.Add(key, value);
        }

        // Body & Attachments
        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        };

        foreach (var attachment in message.Attachments)
        {
            var contentType = ContentType.Parse(attachment.ContentType);
            builder.Attachments.Add(attachment.FileName, attachment.Content, contentType);
        }

        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }
}
