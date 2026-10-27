using System;
using System.Collections.Generic;

namespace Security.Application.DTOs;

public class RefreshResult
{
    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public string? AccessToken { get; set; }

    public DateTime? AccessTokenExpiresAt { get; set; }

    public string? NewRefreshToken { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    public Guid? UserId { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public Guid? TenantId { get; set; }

    public List<string> Roles { get; set; } = new();

    public List<string> Permissions { get; set; } = new();

    public Guid? SessionId { get; set; }
}
