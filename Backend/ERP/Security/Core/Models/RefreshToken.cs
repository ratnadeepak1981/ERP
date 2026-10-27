namespace Security.Core.Models;

using System;
using Security.Core.Common;

public class RefreshToken : Auditable
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public UserSession UserSession { get; set; } = null!;

    public Guid TokenFamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public int SequenceNumber { get; set; } = 1;

    public DateTime ExpiresAt { get; set; }

    public bool IsConsumed { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }
}
