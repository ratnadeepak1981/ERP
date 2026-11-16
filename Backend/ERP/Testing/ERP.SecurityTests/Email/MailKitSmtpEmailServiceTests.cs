using System;
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

        public Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
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
            if (ShouldThrowOnAuthenticate)
                throw new MailKit.Security.AuthenticationException("Invalid Gmail credentials. 535-5.7.8 Username and Password not accepted.");

            AuthenticateCalled = true;
            AuthenticatedUser = userName;
            AuthenticatedPassword = password;
            return Task.CompletedTask;
        }

        public Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnSend)
                throw new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "Mailbox quota exceeded.");

            SendCalled = true;
            SentMessage = message;
            return Task.FromResult("250 2.0.0 OK queued as 778912");
        }

        public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        {
            DisconnectCalled = true;
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
        Assert.Contains("Invalid Gmail credentials", result.ErrorMessage);
        Assert.DoesNotContain("test-app-password", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_TransportFailure_ReturnsFailureAndDisposesClient()
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
        Assert.Contains("Mailbox quota exceeded", result.ErrorMessage);
        Assert.True(factory.Client.Disposed);
    }
}
