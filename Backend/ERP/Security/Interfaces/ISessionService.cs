using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Security.Application.DTOs;
using Security.Core.Models;

namespace Security.Interfaces;

public interface ISessionService
{
    Task<(UserSession session, string rawRefreshToken)> CreateSessionAsync(
        Guid userId,
        Guid? tenantId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<RefreshResult> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task RevokeSessionByTokenAsync(
        string rawRefreshToken,
        string reason = "UserLogout",
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSessionByIdAsync(
        Guid sessionId,
        Guid requestingUserId,
        bool isPlatformAdmin,
        string reason = "SessionRevoked",
        CancellationToken cancellationToken = default);

    Task RevokeAllSessionsForUserAsync(
        Guid userId,
        string reason = "LogoutAll",
        CancellationToken cancellationToken = default);

    Task<List<SessionDto>> GetActiveSessionsForUserAsync(
        Guid userId,
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default);
}
