using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Security.Interfaces;

namespace API.Security;

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserScopeService _userScopeService;

    private IReadOnlyCollection<Guid>? _companyIds;
    private IReadOnlyCollection<Guid>? _branchIds;

    public CurrentUserContext(
        IHttpContextAccessor httpContextAccessor,
        IUserScopeService userScopeService)
    {
        _httpContextAccessor = httpContextAccessor;
        _userScopeService = userScopeService;
    }

    public Guid UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            var value =
                user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);

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
            {
                return tenantId;
            }

            return null;
        }
    }

    public bool IsPlatformUser =>
        _httpContextAccessor.HttpContext?
            .User
            .IsInRole("Platform Admin") == true;

    public bool IsTenantUser =>
        TenantId.HasValue;

    public IReadOnlyCollection<Guid> CompanyIds =>
        _companyIds ??= LoadCompanyIds();

    public IReadOnlyCollection<Guid> BranchIds =>
        _branchIds ??= LoadBranchIds();

    private IReadOnlyCollection<Guid> LoadCompanyIds()
    {
        return _userScopeService
            .GetCompanyIdsAsync(UserId)
            .GetAwaiter()
            .GetResult();
    }

    private IReadOnlyCollection<Guid> LoadBranchIds()
    {
        return _userScopeService
            .GetBranchIdsAsync(UserId)
            .GetAwaiter()
            .GetResult();
    }
}