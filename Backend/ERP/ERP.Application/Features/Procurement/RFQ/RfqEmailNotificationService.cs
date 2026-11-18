using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Procurement.RequestForQuotation;
using ERP.Application.Common.Email;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Procurement.RFQ;

public class RfqEmailNotificationService : IRfqEmailNotificationService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<RfqEmailNotificationService>? _logger;

    public RfqEmailNotificationService(
        IEmailService emailService,
        ILogger<RfqEmailNotificationService>? logger = null)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<RfqEmailDispatchResult> SendInvitationAsync(
        RfqInvitationEmailContext context,
        CancellationToken ct = default)
    {
        var subject = $"Request for Quotation Invitation: {context.RfqNumber} - {context.RfqTitle}";
        var body = BuildEmailHtml(context);

        var message = new EmailMessage
        {
            Subject = subject,
            HtmlBody = body
        };
        message.To.Add(new EmailRecipient(context.RecipientEmail, context.RecipientName));

        var result = await _emailService.SendEmailAsync(message, ct);
        return new RfqEmailDispatchResult(
            result.IsSuccess,
            result.ErrorMessage);
    }

    public Task<RfqEmailDispatchResult> SendInvitationAsync(
        string recipientEmail,
        string recipientName,
        string rfqNumber,
        string rfqTitle,
        string submissionUrl,
        DateTime responseDeadline,
        CancellationToken ct = default)
    {
        var context = new RfqInvitationEmailContext
        {
            RecipientEmail = recipientEmail,
            RecipientName = recipientName,
            RfqNumber = rfqNumber,
            RfqTitle = rfqTitle,
            StandardLocalhostUrl = submissionUrl,
            ResponseDeadline = responseDeadline
        };
        return SendInvitationAsync(context, ct);
    }

    private static string BuildEmailHtml(RfqInvitationEmailContext context)
    {
        var sb = new StringBuilder();
        var companyText = !string.IsNullOrWhiteSpace(context.BuyingCompanyName)
            ? context.BuyingCompanyName
            : "ApexERP Procurement";

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <title>RFQ Invitation - {context.RfqNumber}</title>
</head>
<body style=""margin:0; padding:24px; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color:#f1f5f9; color:#0f172a;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:680px; margin:0 auto; background-color:#ffffff; border:1px solid #e2e8f0; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.06);"">
    <!-- Header -->
    <tr>
      <td style=""padding:24px 32px; background:linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color:#ffffff;"">
        <span style=""display:inline-block; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; background-color:rgba(255,255,255,0.18); color:#f8fafc; padding:3px 8px; border-radius:4px; margin-bottom:10px;"">Request for Quotation</span>
        <h1 style=""margin:0 0 6px 0; font-size:22px; font-weight:700; color:#ffffff;"">{context.RfqNumber}</h1>
        <p style=""margin:0; font-size:15px; color:#cbd5e1;"">{context.RfqTitle}</p>
      </td>
    </tr>

    <!-- Content -->
    <tr>
      <td style=""padding:28px 32px;"">
        <p style=""margin:0 0 14px 0; font-size:15px; line-height:1.5;"">Dear <strong>{context.RecipientName}</strong>,</p>
        <p style=""margin:0 0 16px 0; font-size:14px; line-height:1.5; color:#334155;"">
          You have been formally invited by <strong>{companyText}</strong> to submit your quotation for <strong>{context.RfqNumber} - {context.RfqTitle}</strong>.
        </p>

        <!-- Deadline Box -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:18px 0; background-color:#f8fafc; border-left:4px solid #0f6cbd; border-radius:0 4px 4px 0; padding:12px 16px;"">
          <tr>
            <td style=""font-size:13px; color:#1e293b;"">
              <strong>Submission Deadline:</strong> {context.ResponseDeadline:yyyy-MM-dd HH:mm} UTC
            </td>
          </tr>
        </table>");

        if (!string.IsNullOrWhiteSpace(context.CustomNote))
        {
            sb.Append($@"
        <div style=""margin:16px 0; background-color:#fffbeb; border:1px solid #fef3c7; border-left:4px solid #f59e0b; padding:12px 16px; border-radius:0 4px 4px 0; font-size:13px; color:#92400e;"">
          <strong>Buyer Instructions:</strong> {context.CustomNote}
        </div>");
        }

        sb.Append(@"
        <h3 style=""margin:28px 0 8px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Quotation Portal Access Options
        </h3>
        <p style=""margin:0 0 18px 0; font-size:13px; color:#64748b;"">
          Please select any of the authorized portal access options below to review requirements and submit your quotation:
        </p>");

        // --- OPTION 1: Standard Localhost ---
        sb.Append($@"
        <!-- Option 1: Standard Localhost -->
        <div style=""margin-bottom:18px; padding:16px; border:1px solid #e2e8f0; border-radius:6px; background-color:#ffffff;"">
          <div style=""font-size:14px; font-weight:700; color:#0f172a; margin-bottom:4px;"">
            Option 1: Standard Localhost Portal
          </div>
          <div style=""font-size:12px; color:#64748b; margin-bottom:10px;"">
            Direct access on the standard shared workstation host.
          </div>
          <div style=""margin-bottom:10px;"">
            <a href=""{context.StandardLocalhostUrl}"" target=""_blank"" style=""display:inline-block; padding:10px 20px; background-color:#0d6efd; color:#ffffff; text-decoration:none; border-radius:4px; font-size:13px; font-weight:600;"">
              Open via Standard Localhost
            </a>
          </div>
          <div style=""font-size:12px; color:#475569; word-break:break-all;"">
            <strong>Direct Link:</strong> <a href=""{context.StandardLocalhostUrl}"" style=""color:#0d6efd;"">{context.StandardLocalhostUrl}</a>
          </div>
        </div>");

        // --- OPTION 2: Localhost Tenant Subdomain ---
        sb.Append(@"
        <!-- Option 2: Localhost Tenant Subdomain -->
        <div style=""margin-bottom:18px; padding:16px; border:1px solid #e2e8f0; border-radius:6px; background-color:#ffffff;"">
          <div style=""font-size:14px; font-weight:700; color:#0f172a; margin-bottom:4px;"">
            Option 2: Localhost Tenant Subdomain Portal
          </div>
          <div style=""font-size:12px; color:#64748b; margin-bottom:10px;"">
            Access via the organization's dedicated local tenant subdomain (*.localhost).
          </div>");

        if (!string.IsNullOrWhiteSpace(context.TenantLocalhostUrl))
        {
            sb.Append($@"
          <div style=""margin-bottom:10px;"">
            <a href=""{context.TenantLocalhostUrl}"" target=""_blank"" style=""display:inline-block; padding:10px 20px; background-color:#059669; color:#ffffff; text-decoration:none; border-radius:4px; font-size:13px; font-weight:600;"">
              Open via Tenant Subdomain (Localhost)
            </a>
          </div>
          <div style=""font-size:12px; color:#475569; word-break:break-all;"">
            <strong>Direct Link:</strong> <a href=""{context.TenantLocalhostUrl}"" style=""color:#059669;"">{context.TenantLocalhostUrl}</a>
          </div>");
        }
        else
        {
            sb.Append($@"
          <div style=""padding:8px 12px; background-color:#fef2f2; border:1px solid #fecaca; border-radius:4px; font-size:12px; color:#991b1b;"">
            <strong>Option Unavailable:</strong> {context.TenantLocalhostUnavailableReason ?? "Tenant subdomain not configured."}
          </div>");
        }
        sb.Append("</div>");

        // --- OPTION 3: External Tenant Access ---
        sb.Append(@"
        <!-- Option 3: External Tenant Access -->
        <div style=""margin-bottom:20px; padding:16px; border:1px solid #e2e8f0; border-radius:6px; background-color:#ffffff;"">
          <div style=""font-size:14px; font-weight:700; color:#0f172a; margin-bottom:4px;"">
            Option 3: External / Hosted Tenant Portal
          </div>
          <div style=""font-size:12px; color:#64748b; margin-bottom:10px;"">
            External network access via configured hosted tenant domain or LAN IP.
          </div>");

        if (!string.IsNullOrWhiteSpace(context.ExternalTenantUrl))
        {
            sb.Append($@"
          <div style=""margin-bottom:10px;"">
            <a href=""{context.ExternalTenantUrl}"" target=""_blank"" style=""display:inline-block; padding:10px 20px; background-color:#4f46e5; color:#ffffff; text-decoration:none; border-radius:4px; font-size:13px; font-weight:600;"">
              Open via External / Cloud Portal
            </a>
          </div>
          <div style=""font-size:12px; color:#475569; word-break:break-all;"">
            <strong>Direct Link:</strong> <a href=""{context.ExternalTenantUrl}"" style=""color:#4f46e5;"">{context.ExternalTenantUrl}</a>
          </div>
          <p style=""margin:6px 0 0 0; font-size:11px; color:#64748b;"">
            <em>(Note: External DNS or local hosts entry required to resolve external domain in test environments)</em>
          </p>");
        }
        else
        {
            sb.Append($@"
          <div style=""padding:8px 12px; background-color:#fef2f2; border:1px solid #fecaca; border-radius:4px; font-size:12px; color:#991b1b;"">
            <strong>Option Unavailable:</strong> {context.ExternalTenantUnavailableReason ?? "External tenant host not configured."}
          </div>");
        }
        sb.Append("</div>");

        // Footer
        sb.Append($@"
        <div style=""border-top:1px solid #e2e8f0; padding-top:16px; margin-top:24px; font-size:12px; color:#64748b; line-height:1.5;"">
          <p style=""margin:0 0 8px 0;"">
            <strong>Security Notice:</strong> The links above contain a secure, single-use token issued exclusively for {context.RecipientName}. Do not forward or distribute this email.
          </p>
          <p style=""margin:0;"">
            Thank you,<br/>
            <strong>{companyText}</strong> Procurement Team
          </p>
        </div>
      </td>
    </tr>
  </table>
</body>
</html>");

        return sb.ToString();
    }
}
