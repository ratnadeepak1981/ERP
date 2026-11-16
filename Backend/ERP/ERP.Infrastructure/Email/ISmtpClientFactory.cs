using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ERP.Infrastructure.Email;

/// <summary>
/// Minimal transport adapter abstraction wrapping MailKit SMTP client operations.
/// Enables 100% testability and provider independence without real network calls in tests.
/// </summary>
public interface ISmtpTransportClient : IDisposable
{
    Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default);
    Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
    Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory interface for creating SMTP transport client instances.
/// </summary>
public interface ISmtpClientFactory
{
    ISmtpTransportClient CreateClient();
}

/// <summary>
/// Default production factory wrapping MailKit SmtpClient.
/// </summary>
public class DefaultSmtpClientFactory : ISmtpClientFactory
{
    public ISmtpTransportClient CreateClient() => new MailKitSmtpTransportClient();
}

/// <summary>
/// Concrete MailKit SmtpClient transport implementation.
/// </summary>
public class MailKitSmtpTransportClient : ISmtpTransportClient
{
    private readonly SmtpClient _client = new();

    public Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        => _client.ConnectAsync(host, port, options, cancellationToken);

    public Task AuthenticateAsync(string userName, string password, CancellationToken cancellationToken = default)
        => _client.AuthenticateAsync(userName, password, cancellationToken);

    public Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
        => _client.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default)
        => _client.DisconnectAsync(quit, cancellationToken);

    public void Dispose() => _client.Dispose();
}
