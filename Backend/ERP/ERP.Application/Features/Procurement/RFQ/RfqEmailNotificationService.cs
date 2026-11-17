using System;
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
        string recipientEmail,
        string recipientName,
        string rfqNumber,
        string rfqTitle,
        string submissionUrl,
        DateTime responseDeadline,
        CancellationToken ct = default)
    {
        var subject = $"Request for Quotation Invitation: {rfqNumber} - {rfqTitle}";
        var body = $@"
<h2>Request for Quotation Invitation</h2>
<p>Dear {recipientName},</p>
<p>You have been formally invited to submit a quotation for <strong>{rfqNumber} - {rfqTitle}</strong>.</p>
<p><strong>Submission Deadline:</strong> {responseDeadline:yyyy-MM-dd HH:mm} UTC</p>
<p>Please submit your competitive quotation via our secure portal by clicking the link below:</p>
<p><a href=""{submissionUrl}"" style=""display:inline-block;padding:10px 20px;background-color:#0d6efd;color:#ffffff;text-decoration:none;border-radius:4px;"">Submit Quotation</a></p>
<p><small>Link: {submissionUrl}</small></p>
<p>Thank you,<br/>Procurement Department</p>";

        var message = new EmailMessage
        {
            Subject = subject,
            HtmlBody = body
        };
        message.To.Add(new EmailRecipient(recipientEmail, recipientName));

        var result = await _emailService.SendEmailAsync(message, ct);
        return new RfqEmailDispatchResult(
            result.IsSuccess,
            result.ErrorMessage);
    }
}
