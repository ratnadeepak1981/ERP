using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Common.Email;

/// <summary>
/// Centralised email delivery abstraction for all ApexERP modules.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Dispatches an email message asynchronously using the configured transport mechanism.
    /// </summary>
    /// <param name="message">The validated email message model.</param>
    /// <param name="cancellationToken">Cancellation token for aborting long network operations.</param>
    /// <returns>Result indicating success or structured failure error.</returns>
    Task<EmailResult> SendEmailAsync(
        EmailMessage message, 
        CancellationToken cancellationToken = default);
}
