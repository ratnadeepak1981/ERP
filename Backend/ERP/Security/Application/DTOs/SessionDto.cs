using System;

namespace Security.Application.DTOs;

public class SessionDto
{
    public Guid SessionId { get; set; }

    public Guid? TenantId { get; set; }

    public string? DeviceName { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime AbsoluteExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public bool IsCurrentSession { get; set; }
}
