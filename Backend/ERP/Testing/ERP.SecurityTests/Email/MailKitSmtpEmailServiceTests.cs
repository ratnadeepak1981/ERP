using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using ERP.Application.Common.Email;
using ERP.Infrastructure.Email;
using ERP.Infrastructure.Email.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace ERP.SecurityTests.Email;

public class MailKitSmtpEmailServiceTests
{
    private class FakeSmtpClient : ISmtpTransportClient
    {
        public bool ConnectCalled { get; private set; }
        public bool AuthenticateCalled { get; private set; }
        public bool SendCalled { get; private set; }
        public bool DisconnectCalled { get; private set; }
        public bool Disposed { get; private set; }

        public string? ConnectedHost { get; private set; }
        public int ConnectedPort { get; private set; }
        public SecureSocketOptions ConnectedSslOptions { get; private set; }

        public string? AuthenticatedUser { get; private set; }
        public string? AuthenticatedPassword { get; private set; }

        public MimeMessage? SentMessage { get; private set; }

        public bool ShouldThrowOnConnect { get; set; }
        public bool ShouldThrowOnAuthenticate { get; set; }
        public bool ShouldThrowOnSend { get; set; }
        public bool ShouldThrowOnDisconnect { get; set; }

        public Exception? CustomConnectException { get; set; }
        public Exception? CustomAuthenticateException { get; set; }
        public Exception? CustomSendException { get; set; }
        public Exception? CustomDisconnectException { get; set; }

        public Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (CustomConnectException != null)
                throw CustomConnectException;

            if (ShouldThrowOnConnect)
                throw new System.Security.Authentication.AuthenticationException("Failed to negotiate TLS on smtp.gmail.com:587.");

            ConnectCalled = true;
            ConnectedHost = host;
            ConnectedPort = port;
            ConnectedSslOptions = options;
            return Task.CompletedTask;
        }

        public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (CustomAuthenticateException != null)
                throw CustomAuthenticateException;

            if (ShouldThrowOnAuthenticate)
                throw new MailKit.Security.AuthenticationException("Invalid Gmail credentials. 535-5.7.8 Username and Password not accepted.");

            AuthenticateCalled = true;
            AuthenticatedUser = userName;
            AuthenticatedPassword = password;
            return Task.CompletedTask;
        }

        public Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (CustomSendException != null)
                throw CustomSendException;

            if (ShouldThrowOnSend)
                throw new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "Mailbox quota exceeded.");

            SendCalled = true;
            SentMessage = message;
            return Task.FromResult("250 2.0.0 OK queued as 778912");
        }

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            DisconnectCalled = true;

            if (CustomDisconnectException != null)
                throw CustomDisconnectException;

            if (ShouldThrowOnDisconnect)
                throw new SocketException((int)SocketError.ConnectionReset);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private class FakeSmtpClientFactory : ISmtpClientFactory
    {
        public FakeSmtpClient Client { get; } = new();
        public ISmtpTransportClient CreateClient() => Client;
    }

    private readonly SmtpOptions _validOptions = new()
    {
        Host = "smtp.gmail.com",
        Port = 587,
        EnableSsl = true,
        SenderEmail = "procurement@company.com",
        SenderDisplayName = "ApexERP Procurement",
        Username = "procurement@gmail.com",
        Password = "test-app-password"
    };

    [Fact]
    public async Task SendEmailAsync_SuccessfulTransmission_ConnectsAuthenticatesAndSendsMessage()
    {
        var factory = new FakeSmtpClientFactory();
        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Subject",
            HtmlBody = "<h1>Hello</h1>",
            PlainTextBody = "Hello"
        };
        message.To.Add(new EmailRecipient("supplier@example.com", "Supplier Name"));
        message.Cc.Add(new EmailRecipient("buyer@company.local"));
        message.Attachments.Add(new EmailAttachment("PO.pdf", "application/pdf", new byte[] { 10, 20, 30 }));

        var result = await service.SendEmailAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(EmailFailureCategory.None, result.FailureCategory);
        Assert.True(factory.Client.ConnectCalled);
        Assert.Equal("smtp.gmail.com", factory.Client.ConnectedHost);
        Assert.Equal(587, factory.Client.ConnectedPort);
        Assert.Equal(SecureSocketOptions.StartTls, factory.Client.ConnectedSslOptions);

        Assert.True(factory.Client.AuthenticateCalled);
        Assert.Equal("procurement@gmail.com", factory.Client.AuthenticatedUser);
        Assert.Equal("test-app-password", factory.Client.AuthenticatedPassword);

        Assert.True(factory.Client.SendCalled);
        Assert.NotNull(factory.Client.SentMessage);
        Assert.Equal("Test Subject", factory.Client.SentMessage!.Subject);
        Assert.True(factory.Client.DisconnectCalled);
        Assert.True(factory.Client.Disposed);
    }

    [Fact]
    public async Task SendEmailAsync_DisconnectFailureAfterSend_ReturnsSuccessAndLogsWarning()
    {
        // Scenario 10: SMTP send succeeds but disconnect fails -> email result must remain successful
        var factory = new FakeSmtpClientFactory();
        factory.Client.ShouldThrowOnDisconnect = true;

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Disconnect Cleanup Failure",
            HtmlBody = "<p>Payload</p>"
        };
        message.To.Add(new EmailRecipient("recipient@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(EmailFailureCategory.None, result.FailureCategory);
        Assert.True(factory.Client.SendCalled);
        Assert.True(factory.Client.DisconnectCalled);
        Assert.True(factory.Client.Disposed);
    }

    [Fact]
    public async Task SendEmailAsync_AuthenticationFailure_ReturnsFailureWithoutLeakingPassword()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.ShouldThrowOnAuthenticate = true;

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Auth",
            HtmlBody = "<p>Auth test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(EmailFailureCategory.PermanentAuthentication, result.FailureCategory);
        Assert.Contains("Invalid Gmail credentials", result.ErrorMessage);
        Assert.DoesNotContain("test-app-password", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_TlsSecurityFailure_ReturnsPermanentSecurityCategory()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.ShouldThrowOnConnect = true;

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test TLS",
            HtmlBody = "<p>TLS test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentSecurity, result.FailureCategory);
        Assert.Contains("TLS security handshake failed", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_TransportFailure5xx_ReturnsPermanentMailbox()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.ShouldThrowOnSend = true;

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Fail",
            HtmlBody = "<p>Fail test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentMailbox, result.FailureCategory);
        Assert.Contains("Mailbox quota exceeded", result.ErrorMessage);
        Assert.True(factory.Client.Disposed);
    }

    [Fact]
    public async Task SendEmailAsync_TransportFailure4xx_ReturnsTransientServer()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.CustomSendException = new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted, 
            SmtpStatusCode.MailboxBusy, 
            "450 4.2.1 Mailbox temporarily busy, try again later.");

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test 4xx Transient",
            HtmlBody = "<p>4xx test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.TransientServer, result.FailureCategory);
        Assert.Contains("Mailbox temporarily busy", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_SmtpProtocolFailure_ReturnsAmbiguousCategory()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.CustomSendException = new SmtpProtocolException("Connection unexpectedly terminated during DATA transmission.");

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Protocol Drop",
            HtmlBody = "<p>Protocol test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.Ambiguous, result.FailureCategory);
        Assert.Contains("SMTP protocol failure", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_SocketException_ReturnsTransientNetworkCategory()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.CustomConnectException = new SocketException((int)SocketError.TimedOut);

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Socket Drop",
            HtmlBody = "<p>Socket test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.TransientNetwork, result.FailureCategory);
        Assert.Contains("Network connection failure", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_TimeoutException_ReturnsTransientNetworkCategory()
    {
        var factory = new FakeSmtpClientFactory();
        factory.Client.CustomConnectException = new TimeoutException("The connection attempt timed out after 15000ms.");

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Timeout",
            HtmlBody = "<p>Timeout test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.TransientNetwork, result.FailureCategory);
        Assert.Contains("SMTP connection timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_CancellationTokenCanceled_ReturnsCanceledCategory()
    {
        var factory = new FakeSmtpClientFactory();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        var message = new EmailMessage
        {
            Subject = "Test Canceled",
            HtmlBody = "<p>Cancel test</p>"
        };
        message.To.Add(new EmailRecipient("test@example.com"));

        var result = await service.SendEmailAsync(message, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.Canceled, result.FailureCategory);
        Assert.Contains("canceled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendEmailAsync_ValidationFailure_ReturnsPermanentValidationCategory()
    {
        var factory = new FakeSmtpClientFactory();
        var service = new MailKitSmtpEmailService(
            Options.Create(_validOptions),
            NullLogger<MailKitSmtpEmailService>.Instance,
            factory);

        // Missing To recipient violates EmailMessage.Validate()
        var message = new EmailMessage
        {
            Subject = "Missing Recipients",
            HtmlBody = "<p>Empty To</p>"
        };

        var result = await service.SendEmailAsync(message);

        Assert.False(result.IsSuccess);
        Assert.Equal(EmailFailureCategory.PermanentValidation, result.FailureCategory);
        Assert.Contains("Validation failed", result.ErrorMessage);
    }
}
