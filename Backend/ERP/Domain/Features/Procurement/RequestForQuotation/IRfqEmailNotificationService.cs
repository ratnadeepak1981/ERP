using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.RequestForQuotation;

public sealed record RfqEmailDispatchResult(bool IsSuccess, string? FailureReason = null);

public interface IRfqEmailNotificationService
{
    Task<RfqEmailDispatchResult> SendInvitationAsync(
        string recipientEmail,
        string recipientName,
        string rfqNumber,
        string rfqTitle,
        string submissionUrl,
        DateTime responseDeadline,
        CancellationToken ct = default);
}
