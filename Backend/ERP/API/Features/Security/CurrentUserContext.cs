using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Security.Interfaces;

namespace API.Security;

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!Guid.TryParse(value, out var userId))
            {
                throw new InvalidOperationException(
                    "Authenticated user ID is missing.");
            }

            return userId;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue("tenant_id");

            if (Guid.TryParse(value, out var tenantId))
                return tenantId;

            return null;
        }
    }

    public bool IsPlatformUser =>
        TenantId == null;

    public bool IsTenantUser =>
        TenantId.HasValue;
}