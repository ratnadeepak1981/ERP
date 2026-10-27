using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Security.Application.DTOs;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Infrastructure.Persistence.Auditing;
using Security.Interfaces;
using Security.Options;

namespace Security.Services;

public class SessionService : ISessionService
{
    private readonly SecurityDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IUserRepository _userRepository;
    private readonly AuthenticationOptions _authOptions;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        SecurityDbContext dbContext,
        ITokenService tokenService,
        IUserRepository userRepository,
        IOptions<AuthenticationOptions>? authOptions,
        ILogger<SessionService> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _userRepository = userRepository;
        _authOptions = authOptions?.Value ?? new AuthenticationOptions();
        _logger = logger;
    }

    public async Task<(UserSession session, string rawRefreshToken)> CreateSessionAsync(
        Guid userId,
        Guid? tenantId,
        string? deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tokenFamilyId = Guid.NewGuid();
        var absoluteExpiresAt = now.AddDays(_authOptions.RefreshTokenAbsoluteLifetimeDays);

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenFamilyId = tokenFamilyId,
            DeviceName = deviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            LastActivityAt = now,
            AbsoluteExpiresAt = absoluteExpiresAt,
            IsRevoked = false
        };

        string rawRefreshToken = _tokenService.GenerateRefreshToken();
        string tokenHash = _tokenService.HashToken(rawRefreshToken);

        var idleExpiresAt = now.AddDays(_authOptions.RefreshTokenIdleLifetimeDays);
        var tokenExpiresAt = idleExpiresAt > absoluteExpiresAt ? absoluteExpiresAt : idleExpiresAt;

        var tokenRecord = new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            TokenFamilyId = tokenFamilyId,
            TokenHash = tokenHash,
            SequenceNumber = 1,
            ExpiresAt = tokenExpiresAt,
            IsConsumed = false,
            IsRevoked = false
        };

        await _dbContext.UserSessions.AddAsync(session, cancellationToken);
        await _dbContext.RefreshTokens.AddAsync(tokenRecord, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User session {SessionId} created for user {UserId} with token family {TokenFamilyId}.",
            session.Id,
            userId,
            tokenFamilyId);

        return (session, rawRefreshToken);
    }

    public async Task<RefreshResult> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Refresh token is missing."
            };
        }

        string incomingHash = _tokenService.HashToken(rawRefreshToken);

        var token = await _dbContext.RefreshTokens
            .Include(rt => rt.UserSession)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash, cancellationToken);

        if (token == null)
        {
            _logger.LogWarning("Refresh token hash not found in database.");
            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Invalid refresh token."
            };
        }

        var now = DateTime.UtcNow;

        // 1. Replay Detection: Check if token was already consumed
        if (token.IsConsumed)
        {
            _logger.LogWarning(
                "SECURITY ALERT: Consumed refresh token reused! TokenId: {TokenId}, Family: {FamilyId}, Session: {SessionId}.",
                token.Id,
                token.TokenFamilyId,
                token.SessionId);

            // Revoke entire token family and the associated session immediately
            var familyTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.TokenFamilyId == token.TokenFamilyId && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var ft in familyTokens)
            {
                ft.IsRevoked = true;
                ft.RevokedAt = now;
                ft.RevokedReason = "ReplayBreachDetected";
            }

            if (!token.UserSession.IsRevoked)
            {
                token.UserSession.IsRevoked = true;
                token.UserSession.RevokedAt = now;
                token.UserSession.RevokedReason = "ReplayBreachDetected";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Invalid refresh token."
            };
        }

        // 2. Revocation status
        if (token.IsRevoked || token.UserSession.IsRevoked)
        {
            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Refresh token has been revoked."
            };
        }

        // 3. Expiration checks
        if (token.ExpiresAt <= now)
        {
            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Refresh token has expired."
            };
        }

        if (token.UserSession.AbsoluteExpiresAt <= now)
        {
            token.UserSession.IsRevoked = true;
            token.UserSession.RevokedAt = now;
            token.UserSession.RevokedReason = "AbsoluteExpiryReached";

            token.IsRevoked = true;
            token.RevokedAt = now;
            token.RevokedReason = "AbsoluteExpiryReached";

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Session has reached absolute lifetime limit."
            };
        }

        // 4. Verify User account active status
        var user = token.UserSession.User;
        if (user == null || !user.IsActive)
        {
            token.UserSession.IsRevoked = true;
            token.UserSession.RevokedAt = now;
            token.UserSession.RevokedReason = "UserDeactivated";

            token.IsRevoked = true;
            token.RevokedAt = now;
            token.RevokedReason = "UserDeactivated";

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "User account is suspended or inactive."
            };
        }

        // 5. Verify Tenant membership
        if (token.UserSession.TenantId != user.TenantId)
        {
            token.UserSession.IsRevoked = true;
            token.UserSession.RevokedAt = now;
            token.UserSession.RevokedReason = "TenantMismatch";

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Tenant membership is invalid."
            };
        }

        // 6. Atomic Consumption and Token Rotation
        token.IsConsumed = true;
        token.ConsumedAt = now;

        string newRawRefreshToken = _tokenService.GenerateRefreshToken();
        string newTokenHash = _tokenService.HashToken(newRawRefreshToken);

        token.ReplacedByTokenHash = newTokenHash;

        var nextIdleExpiry = now.AddDays(_authOptions.RefreshTokenIdleLifetimeDays);
        var nextTokenExpiry = nextIdleExpiry > token.UserSession.AbsoluteExpiresAt
            ? token.UserSession.AbsoluteExpiresAt
            : nextIdleExpiry;

        var successorToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = token.SessionId,
            TokenFamilyId = token.TokenFamilyId,
            TokenHash = newTokenHash,
            SequenceNumber = token.SequenceNumber + 1,
            ExpiresAt = nextTokenExpiry,
            IsConsumed = false,
            IsRevoked = false
        };

        token.UserSession.LastActivityAt = now;
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            token.UserSession.IpAddress = ipAddress;
        }
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            token.UserSession.UserAgent = userAgent;
        }

        await _dbContext.RefreshTokens.AddAsync(successorToken, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException dbEx)
        {
            _logger.LogWarning(dbEx, "Concurrency collision during refresh token rotation.");
            return new RefreshResult
            {
                Succeeded = false,
                ErrorMessage = "Concurrent refresh conflict detected."
            };
        }

        // 7. Re-query current active roles and permissions for authoritative freshness
        var roles = await _userRepository.GetRolesAsync(user.Id);
        var permissions = await _userRepository.GetPermissionsAsync(user.Id);

        string newAccessToken = _tokenService.GenerateAccessToken(
            user.Id,
            user.Username,
            user.TenantId,
            roles,
            permissions);

        int lifetimeMinutes = _tokenService.GetAccessTokenLifetimeMinutes();

        return new RefreshResult
        {
            Succeeded = true,
            AccessToken = newAccessToken,
            AccessTokenExpiresAt = now.AddMinutes(lifetimeMinutes),
            NewRefreshToken = newRawRefreshToken,
            RefreshTokenExpiresAt = nextTokenExpiry,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            TenantId = user.TenantId,
            Roles = roles,
            Permissions = permissions,
            SessionId = token.SessionId
        };
    }

    public async Task RevokeSessionByTokenAsync(
        string rawRefreshToken,
        string reason = "UserLogout",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        string tokenHash = _tokenService.HashToken(rawRefreshToken);
        var token = await _dbContext.RefreshTokens
            .Include(rt => rt.UserSession)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (token == null)
        {
            return;
        }

        var now = DateTime.UtcNow;

        token.IsRevoked = true;
        token.RevokedAt = now;
        token.RevokedReason = reason;

        if (token.UserSession != null && !token.UserSession.IsRevoked)
        {
            token.UserSession.IsRevoked = true;
            token.UserSession.RevokedAt = now;
            token.UserSession.RevokedReason = reason;

            // Revoke all tokens belonging to this session
            var siblingTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.SessionId == token.SessionId && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var st in siblingTokens)
            {
                st.IsRevoked = true;
                st.RevokedAt = now;
                st.RevokedReason = reason;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeSessionByIdAsync(
        Guid sessionId,
        Guid requestingUserId,
        bool isPlatformAdmin,
        string reason = "SessionRevoked",
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return false;
        }

        if (!isPlatformAdmin && session.UserId != requestingUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to revoke a session belonging to another user.");
        }

        var now = DateTime.UtcNow;
        session.IsRevoked = true;
        session.RevokedAt = now;
        session.RevokedReason = reason;

        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.SessionId == sessionId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedAt = now;
            t.RevokedReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllSessionsForUserAsync(
        Guid userId,
        string reason = "LogoutAll",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        var tokens = await _dbContext.RefreshTokens
            .Include(rt => rt.UserSession)
            .Where(rt => rt.UserSession.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.IsRevoked = true;
            t.RevokedAt = now;
            t.RevokedReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("All sessions revoked for user {UserId}. Reason: {Reason}.", userId, reason);
    }

    public async Task<List<SessionDto>> GetActiveSessionsForUserAsync(
        Guid userId,
        Guid? currentSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.AbsoluteExpiresAt > now)
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => new SessionDto
            {
                SessionId = s.Id,
                TenantId = s.TenantId,
                DeviceName = s.DeviceName,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                CreatedAt = s.CreatedAt,
                LastActivityAt = s.LastActivityAt,
                AbsoluteExpiresAt = s.AbsoluteExpiresAt,
                IsActive = !s.IsRevoked && s.AbsoluteExpiresAt > now,
                IsCurrentSession = currentSessionId.HasValue && s.Id == currentSessionId.Value
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }
}
