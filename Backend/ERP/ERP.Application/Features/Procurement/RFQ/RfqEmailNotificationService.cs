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

    public async Task<RfqEmailDispatchResult> SendAwardNotificationAsync(
        RfqAwardEmailContext context,
        CancellationToken ct = default)
    {
        var subject = $"Quotation Award Confirmation: {context.RfqNumber} - {context.RfqTitle}";
        var body = BuildAwardEmailHtml(context);

        var message = new EmailMessage
        {
            Subject = subject,
            HtmlBody = body
        };
        message.To.Add(new EmailRecipient(context.RecipientEmail, context.WinningSupplierName));

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

        var effectiveUrl = !string.IsNullOrWhiteSpace(context.InvitationUrl)
            ? context.InvitationUrl
            : context.StandardLocalhostUrl;

        sb.Append($@"
        <h3 style=""margin:28px 0 8px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Quotation Submission Portal
        </h3>
        <p style=""margin:0 0 18px 0; font-size:13px; color:#64748b;"">
          Please click the button below to review requirements and submit your quotation securely online:
        </p>
        <div style=""margin-bottom:20px; padding:20px; border:1px solid #e2e8f0; border-radius:6px; background-color:#ffffff; text-align:center;"">
          <div style=""margin-bottom:14px;"">
            <a href=""{effectiveUrl}"" target=""_blank"" style=""display:inline-block; padding:12px 28px; background-color:#0d6efd; color:#ffffff; text-decoration:none; border-radius:4px; font-size:14px; font-weight:600; box-shadow:0 2px 4px rgba(13,110,253,0.2);"">
              Open Quotation Submission Portal
            </a>
          </div>
          <div style=""font-size:12px; color:#475569; word-break:break-all; text-align:left; background-color:#f8fafc; padding:10px 12px; border-radius:4px; border:1px solid #e2e8f0;"">
            <strong>Direct Link:</strong> <a href=""{effectiveUrl}"" style=""color:#0d6efd;"">{effectiveUrl}</a>
          </div>
        </div>");

        // Footer
        sb.Append($@"
        <div style=""border-top:1px solid #e2e8f0; padding-top:16px; margin-top:24px; font-size:12px; color:#64748b; line-height:1.5;"">
          <p style=""margin:0 0 8px 0;"">
            <strong>Security Notice:</strong> The link above contains a secure, single-use token issued exclusively for {context.RecipientName}. Do not forward or distribute this email.
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

    private static string BuildAwardEmailHtml(RfqAwardEmailContext context)
    {
        var sb = new StringBuilder();
        var companyText = !string.IsNullOrWhiteSpace(context.BuyerCompanyName)
            ? context.BuyerCompanyName
            : "ApexERP Procurement";

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <title>Quotation Award Confirmation - {context.RfqNumber}</title>
</head>
<body style=""margin:0; padding:24px; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color:#f1f5f9; color:#0f172a;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:680px; margin:0 auto; background-color:#ffffff; border:1px solid #e2e8f0; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.06);"">
    <!-- Header -->
    <tr>
      <td style=""padding:24px 32px; background:linear-gradient(135deg, #064e3b 0%, #0f172a 100%); color:#ffffff;"">
        <span style=""display:inline-block; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; background-color:rgba(255,255,255,0.2); color:#ecfdf5; padding:3px 8px; border-radius:4px; margin-bottom:10px;"">Quotation Award Notice</span>
        <h1 style=""margin:0 0 6px 0; font-size:22px; font-weight:700; color:#ffffff;"">{context.RfqNumber}</h1>
        <p style=""margin:0; font-size:15px; color:#cbd5e1;"">{context.RfqTitle}</p>
      </td>
    </tr>

    <!-- Content -->
    <tr>
      <td style=""padding:28px 32px;"">
        <p style=""margin:0 0 14px 0; font-size:15px; line-height:1.5;"">Dear <strong>{context.WinningSupplierName}</strong>,</p>
        
        <!-- Congratulations message -->
        <div style=""margin:18px 0; background-color:#f0fdf4; border-left:4px solid #16a34a; border-radius:0 4px 4px 0; padding:16px;"">
          <p style=""margin:0; font-size:15px; line-height:1.5; color:#14532d;"">
            <strong>Congratulations!</strong> We are pleased to formally inform you that your quotation <strong>{context.QuotationReference}</strong> has been selected and approved for award on <strong>{context.RfqNumber} - {context.RfqTitle}</strong> by <strong>{companyText}</strong>.
          </p>
        </div>

        <!-- Award Details Summary -->
        <h3 style=""margin:24px 0 12px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Award Confirmation Details
        </h3>
        <table role=""presentation"" width=""100%"" cellpadding=""6"" cellspacing=""0"" style=""font-size:13px; line-height:1.6; margin-bottom:20px;"">
          <tr>
            <td style=""color:#64748b; width:200px; padding:6px 0;"">RFQ Reference:</td>
            <td style=""color:#0f172a; font-weight:600; padding:6px 0;"">{context.RfqNumber}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:6px 0;"">RFQ Title:</td>
            <td style=""color:#0f172a; padding:6px 0;"">{context.RfqTitle}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:6px 0;"">Winning Supplier:</td>
            <td style=""color:#0f172a; font-weight:600; padding:6px 0;"">{context.WinningSupplierName}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:6px 0;"">Supplier Quotation Ref:</td>
            <td style=""color:#0f172a; font-weight:600; padding:6px 0;"">{context.QuotationReference}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:6px 0;"">Award Confirmation Date:</td>
            <td style=""color:#0f172a; padding:6px 0;"">{context.AwardDate:yyyy-MM-dd HH:mm} UTC</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:6px 0;"">Buyer Organization:</td>
            <td style=""color:#0f172a; padding:6px 0;"">{companyText}</td>
          </tr>
        </table>");

        if (!string.IsNullOrWhiteSpace(context.CustomRemarks))
        {
            sb.Append($@"
        <!-- Buyer Remarks -->
        <div style=""margin:16px 0; background-color:#f8fafc; border:1px solid #e2e8f0; border-left:4px solid #0f6cbd; padding:12px 16px; border-radius:0 4px 4px 0; font-size:13px; color:#1e293b;"">
          <strong>Buyer Approval Remarks:</strong> {context.CustomRemarks}
        </div>");
        }

        sb.Append($@"
        <!-- Next Steps -->
        <h3 style=""margin:24px 0 10px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Next Steps & Purchase Order Communication
        </h3>
        <div style=""padding:14px 16px; background-color:#fffbeb; border:1px solid #fef3c7; border-left:4px solid #f59e0b; border-radius:0 4px 4px 0; margin-bottom:20px;"">
          <p style=""margin:0 0 8px 0; font-size:13px; line-height:1.5; color:#92400e;"">
            <strong>Important Notice:</strong> This notification serves as a formal confirmation of quotation selection and award. Official <strong>Purchase Order (PO)</strong> documentation, delivery logistics, and final fulfillment terms will be communicated separately by our procurement team.
          </p>
          <p style=""margin:0; font-size:12px; line-height:1.5; color:#b45309;"">
            Please await the official Purchase Order before preparing shipment or dispatching goods.
          </p>
        </div>

        <!-- Buyer Contact Information -->
        <h3 style=""margin:24px 0 10px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Buyer Contact Information
        </h3>
        <table role=""presentation"" width=""100%"" cellpadding=""4"" cellspacing=""0"" style=""font-size:13px; line-height:1.5; margin-bottom:20px;"">
          <tr>
            <td style=""color:#64748b; width:200px; padding:4px 0;"">Purchasing Organization:</td>
            <td style=""color:#0f172a; font-weight:600; padding:4px 0;"">{companyText}</td>
          </tr>");

        if (!string.IsNullOrWhiteSpace(context.BuyerContactName))
        {
            sb.Append($@"
          <tr>
            <td style=""color:#64748b; padding:4px 0;"">Procurement Representative:</td>
            <td style=""color:#0f172a; padding:4px 0;"">{context.BuyerContactName}</td>
          </tr>");
        }

        if (!string.IsNullOrWhiteSpace(context.BuyerContactEmail))
        {
            sb.Append($@"
          <tr>
            <td style=""color:#64748b; padding:4px 0;"">Contact Email:</td>
            <td style=""color:#0f172a; padding:4px 0;""><a href=""mailto:{context.BuyerContactEmail}"" style=""color:#0d6efd;"">{context.BuyerContactEmail}</a></td>
          </tr>");
        }

        if (!string.IsNullOrWhiteSpace(context.BuyerContactPhone))
        {
            sb.Append($@"
          <tr>
            <td style=""color:#64748b; padding:4px 0;"">Contact Phone:</td>
            <td style=""color:#0f172a; padding:4px 0;"">{context.BuyerContactPhone}</td>
          </tr>");
        }

        sb.Append($@"
        </table>

        <!-- Footer -->
        <div style=""border-top:1px solid #e2e8f0; padding-top:16px; margin-top:24px; font-size:12px; color:#64748b; line-height:1.5;"">
          <p style=""margin:0 0 8px 0;"">
            <strong>Confidentiality Notice:</strong> This award notification is issued exclusively for {context.WinningSupplierName} and does not disclose competing suppliers' quotations, prices, or evaluation metrics.
          </p>
          <p style=""margin:0;"">
            Thank you for your partnership,<br/>
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
