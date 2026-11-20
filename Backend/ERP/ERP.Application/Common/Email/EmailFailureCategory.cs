namespace ERP.Application.Common.Email;

/// <summary>
/// Categorises email delivery failure modes for structured retry decisioning.
/// Eliminates fragile error message string parsing across transport boundaries.
/// </summary>
public enum EmailFailureCategory
{
    None = 0,                   // Succeeded
    TransientNetwork = 1,       // SocketException, TimeoutException, connection drop -> ELIGIBLE FOR RETRY
    TransientServer = 2,        // SMTP 4xx temporary response (rate limit, busy) -> ELIGIBLE FOR RETRY
    PermanentAuthentication = 3,// SMTP 535 invalid credentials -> NO RETRY
    PermanentSecurity = 4,      // TLS handshake failure, untrusted cert -> NO RETRY
    PermanentMailbox = 5,       // SMTP 5xx (550 mailbox not found, 551 user not local) -> NO RETRY
    PermanentValidation = 6,    // Malformed address format, empty recipient -> NO RETRY
    Canceled = 7,               // OperationCanceledException -> ABORT IMMEDIATELY (NO RETRY)
    Ambiguous = 8               // Protocol drop after DATA sent before 250 OK -> UNCERTAIN OUTCOME (NO AUTOMATIC RETRY)
}
