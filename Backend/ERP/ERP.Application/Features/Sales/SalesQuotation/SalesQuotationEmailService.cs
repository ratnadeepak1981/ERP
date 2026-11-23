using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Common.ApplicationUrl;
using Domain.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Application.Common.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Sales.SalesQuotation;

public class SalesQuotationEmailService : ISalesQuotationEmailService
{
    private readonly IEmailService _emailService;
    private readonly IApplicationUrlService _urlService;
    private readonly DomainDbContext _dbContext;
    private readonly ILogger<SalesQuotationEmailService>? _logger;

    public SalesQuotationEmailService(
        IEmailService emailService,
        IApplicationUrlService urlService,
        DomainDbContext dbContext,
        ILogger<SalesQuotationEmailService>? logger = null)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _urlService = urlService ?? throw new ArgumentNullException(nameof(urlService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger;
    }

    public async Task<SalesQuotationEmailDispatchResult> SendQuotationNotificationAsync(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        CancellationToken ct = default)
    {
        if (quotation == null) throw new ArgumentNullException(nameof(quotation));

        // 1. Resolve Selling Company
        var company = await _dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == quotation.TenantId && c.Id == quotation.CompanyId, ct);

        string companyName = company?.Name ?? "ApexERP Commercial Sales";
        string companyCode = company?.Code ?? string.Empty;

        // 2. Resolve Customer Account
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == quotation.TenantId && c.CustomerId == quotation.CustomerId, ct);

        string customerName = customer?.CustomerName ?? "Valued Customer";
        string customerCode = customer?.CustomerCode ?? string.Empty;

        // 3. Resolve Customer Contact & Email
        string? recipientEmail = null;
        string recipientName = customer?.ContactPerson ?? customerName;

        if (quotation.CustomerContactId.HasValue && quotation.CustomerContactId.Value != Guid.Empty)
        {
            var contact = await _dbContext.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == quotation.TenantId && c.ContactId == quotation.CustomerContactId.Value, ct);

            if (contact == null)
            {
                var custContact = await _dbContext.CustomerContacts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cc => cc.TenantId == quotation.TenantId && cc.CustomerContactId == quotation.CustomerContactId.Value, ct);
                if (custContact != null)
                {
                    contact = await _dbContext.Contacts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.TenantId == quotation.TenantId && c.ContactId == custContact.ContactId, ct);
                }
            }

            if (contact != null)
            {
                if (!string.IsNullOrWhiteSpace(contact.ContactName))
                {
                    recipientName = contact.ContactName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(contact.Email))
                {
                    recipientEmail = contact.Email.Trim();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(recipientEmail) && !string.IsNullOrWhiteSpace(customer?.Email))
        {
            recipientEmail = customer.Email.Trim();
        }

        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return new SalesQuotationEmailDispatchResult(
                false,
                $"No verified contact email address could be resolved for customer '{customerName}' ({customerCode}). Please ensure the customer account or designated contact person has a valid email address configured.");
        }

        // 4. Build absolute URL pointing to Customer Workspace quotation view
        string viewUrl = _urlService.BuildAbsoluteUrl($"/customer-portal/quotations/{quotation.Id}");

        // 5. Build HTML email body reusing RFQ invitation styling
        string htmlBody = BuildQuotationEmailHtml(quotation, companyName, companyCode, customerName, recipientName, viewUrl);

        var message = new EmailMessage
        {
            Subject = $"Commercial Sales Quotation: {quotation.QuotationNumber} - {companyName}",
            HtmlBody = htmlBody
        };
        message.To.Add(new EmailRecipient(recipientEmail, recipientName));

        try
        {
            var result = await _emailService.SendEmailAsync(message, ct);

            if (result.IsSuccess)
            {
                _logger?.LogInformation(
                    "Sales Quotation {QuotationNumber} email successfully dispatched to {RecipientEmail} via SMTP.",
                    quotation.QuotationNumber,
                    recipientEmail);

                return new SalesQuotationEmailDispatchResult(true, null, recipientEmail, viewUrl);
            }
            else
            {
                _logger?.LogWarning(
                    "Sales Quotation {QuotationNumber} email dispatch failed to {RecipientEmail}: {Error}",
                    quotation.QuotationNumber,
                    recipientEmail,
                    result.ErrorMessage);

                return new SalesQuotationEmailDispatchResult(false, result.ErrorMessage ?? "SMTP transport failure", recipientEmail, viewUrl);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Unexpected exception during Sales Quotation {QuotationNumber} email dispatch.", quotation.QuotationNumber);
            return new SalesQuotationEmailDispatchResult(false, ex.Message, recipientEmail, viewUrl);
        }
    }

    private static string BuildQuotationEmailHtml(
        global::Domain.Features.Sales.SalesQuotation.SalesQuotation quotation,
        string companyName,
        string companyCode,
        string customerName,
        string recipientName,
        string viewUrl)
    {
        var sb = new StringBuilder();

        sb.Append($@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <title>Sales Quotation - {quotation.QuotationNumber}</title>
</head>
<body style=""margin:0; padding:24px; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color:#f1f5f9; color:#0f172a;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:680px; margin:0 auto; background-color:#ffffff; border:1px solid #e2e8f0; border-radius:8px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.06);"">
    <!-- Header -->
    <tr>
      <td style=""padding:24px 32px; background:linear-gradient(135deg, #0f172a 0%, #1e293b 100%); color:#ffffff;"">
        <span style=""display:inline-block; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:0.06em; background-color:rgba(255,255,255,0.18); color:#f8fafc; padding:3px 8px; border-radius:4px; margin-bottom:10px;"">Commercial Sales Quotation</span>
        <h1 style=""margin:0 0 6px 0; font-size:22px; font-weight:700; color:#ffffff;"">{quotation.QuotationNumber}</h1>
        <p style=""margin:0; font-size:14px; color:#cbd5e1;"">{companyName} {(string.IsNullOrWhiteSpace(companyCode) ? "" : $"({companyCode})")} â€¢ Revision {quotation.RevisionNumber}</p>
      </td>
    </tr>

    <!-- Body Content -->
    <tr>
      <td style=""padding:28px 32px;"">
        <p style=""margin:0 0 14px 0; font-size:15px; line-height:1.5;"">Dear <strong>{recipientName}</strong>,</p>
        <p style=""margin:0 0 16px 0; font-size:14px; line-height:1.5; color:#334155;"">
          We are pleased to provide you with our official commercial sales quotation proposal on behalf of <strong>{companyName}</strong> for <strong>{customerName}</strong>.
        </p>

        <!-- Expiry Deadline Box -->
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:18px 0; background-color:#f8fafc; border-left:4px solid #0f6cbd; border-radius:0 4px 4px 0; padding:12px 16px;"">
          <tr>
            <td style=""font-size:13px; color:#1e293b;"">
              <strong>Offer Expiry Date:</strong> {quotation.ValidUntilDateUtc:yyyy-MM-dd HH:mm} UTC
              <span style=""display:block; font-size:12px; color:#64748b; margin-top:2px;"">This proposal is fixed and valid until the stated date.</span>
            </td>
          </tr>
        </table>

        <!-- Commercial Terms Table -->
        <h3 style=""margin:20px 0 10px 0; font-size:15px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:6px;"">
          Proposal Summary
        </h3>
        <table role=""presentation"" width=""100%"" cellpadding=""4"" cellspacing=""0"" style=""font-size:13px; line-height:1.5; margin-bottom:20px;"">
          <tr>
            <td style=""color:#64748b; width:180px; padding:5px 0;"">Quotation Reference:</td>
            <td style=""color:#0f172a; font-weight:600; padding:5px 0;"">{quotation.QuotationNumber} (Rev {quotation.RevisionNumber})</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:5px 0;"">Customer Account:</td>
            <td style=""color:#0f172a; padding:5px 0;"">{customerName}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:5px 0;"">Total Commercial Value:</td>
            <td style=""color:#0f172a; font-weight:700; font-size:15px; color:#0f6cbd; padding:5px 0;"">{quotation.TotalAmount:N2} {quotation.Currency}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:5px 0;"">Payment Terms:</td>
            <td style=""color:#0f172a; padding:5px 0;"">{(!string.IsNullOrWhiteSpace(quotation.PaymentTerms) ? quotation.PaymentTerms : "Standard Commercial Terms")}</td>
          </tr>
          <tr>
            <td style=""color:#64748b; padding:5px 0;"">Delivery Terms:</td>
            <td style=""color:#0f172a; padding:5px 0;"">{(!string.IsNullOrWhiteSpace(quotation.DeliveryTerms) ? quotation.DeliveryTerms : "Standard Delivery Logistics")}</td>
          </tr>");

        if (!string.IsNullOrWhiteSpace(quotation.DeliveryAddressSnapshot))
        {
            sb.Append($@"
          <tr>
            <td style=""color:#64748b; padding:5px 0;"">Delivery Destination:</td>
            <td style=""color:#0f172a; padding:5px 0;"">{quotation.DeliveryAddressSnapshot}</td>
          </tr>");
        }

        sb.Append(@"
        </table>");

        if (!string.IsNullOrWhiteSpace(quotation.Notes))
        {
            sb.Append($@"
        <div style=""margin:16px 0; background-color:#fffbeb; border:1px solid #fef3c7; border-left:4px solid #f59e0b; padding:12px 16px; border-radius:0 4px 4px 0; font-size:13px; color:#92400e;"">
          <strong>Commercial Notes &amp; Conditions:</strong> {quotation.Notes}
        </div>");
        }

        // View Quotation Portal Box
        sb.Append($@"
        <h3 style=""margin:28px 0 8px 0; font-size:16px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:8px;"">
          Review &amp; Access Your Quotation
        </h3>
        <p style=""margin:0 0 18px 0; font-size:13px; color:#64748b;"">
          Please click the button below to review item details, product specifications, and download the official PDF proposal in your secure Customer Workspace:
        </p>
        <div style=""margin-bottom:24px; padding:22px; border:1px solid #e2e8f0; border-radius:6px; background-color:#ffffff; text-align:center;"">
          <div style=""margin-bottom:14px;"">
            <a href=""{viewUrl}"" target=""_blank"" style=""display:inline-block; padding:12px 32px; background-color:#0d6efd; color:#ffffff; text-decoration:none; border-radius:4px; font-size:14px; font-weight:600; box-shadow:0 2px 4px rgba(13,110,253,0.25);"">
              View Quotation
            </a>
          </div>
          <div style=""font-size:12px; color:#475569; word-break:break-all; text-align:left; background-color:#f8fafc; padding:10px 12px; border-radius:4px; border:1px solid #e2e8f0;"">
            <strong>Direct Link:</strong> <a href=""{viewUrl}"" style=""color:#0d6efd;"">{viewUrl}</a>
          </div>
        </div>

        <!-- Next Steps -->
        <div style=""padding:14px 16px; background-color:#f8fafc; border:1px solid #e2e8f0; border-radius:4px; margin-bottom:20px; font-size:12px; color:#475569; line-height:1.5;"">
          <strong>Customer Account Access:</strong> If you are not currently signed in, you will be prompted to log in using your registered company contact credentials to verify account ownership.
        </div>

        <!-- Selling Company Details -->
        <h3 style=""margin:24px 0 8px 0; font-size:14px; font-weight:700; color:#0f172a; border-bottom:1px solid #e2e8f0; padding-bottom:6px;"">
          Selling Organization Information
        </h3>
        <table role=""presentation"" width=""100%"" cellpadding=""3"" cellspacing=""0"" style=""font-size:12px; color:#475569; margin-bottom:20px;"">
          <tr>
            <td style=""width:160px; color:#64748b;"">Company:</td>
            <td style=""font-weight:600; color:#0f172a;"">{companyName}</td>
          </tr>
        </table>

        <!-- Footer -->
        <div style=""border-top:1px solid #e2e8f0; padding-top:16px; margin-top:24px; font-size:12px; color:#64748b; line-height:1.5;"">
          <p style=""margin:0 0 8px 0;"">
            <strong>Security Notice:</strong> The link above is intended exclusively for authorized contacts of {customerName}. Do not forward or share this notification.
          </p>
          <p style=""margin:0;"">
            Thank you for your business,<br/>
            <strong>{companyName}</strong> Commercial Sales Team
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
