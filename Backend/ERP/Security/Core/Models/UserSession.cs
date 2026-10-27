namespace Security.Core.Models;

using System;
using System.Collections.Generic;
using Security.Core.Common;

public class UserSession : Auditable
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public Guid TokenFamilyId { get; set; }

    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime LastActivityAt { get; set; }
    public DateTime AbsoluteExpiresAt { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
