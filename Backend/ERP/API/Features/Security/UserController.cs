using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;
using Security.Services;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public UserController(
        IUserService userService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _userService = userService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
    }

    [HttpGet("access-test/company/{companyId:guid}")]
    public async Task<IActionResult> TestCompanyAccess(
        Guid companyId)
    {
        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing."
            });
        }

        var canAccess =
            await _userAccessService.CanAccessCompanyAsync(
                _currentUserContext.UserId,
                tenantId.Value,
                companyId);

        return Ok(new
        {
            status = canAccess ? "PASS" : "DENY",
            userId = _currentUserContext.UserId,
            tenantId = tenantId.Value,
            companyId,
            canAccess
        });
    }

    [HttpGet("context")]
    public IActionResult GetContext()
    {
        return Ok(new
        {
            userId = _currentUserContext.UserId,
            tenantId = _currentUserContext.TenantId,
            isPlatformUser = _currentUserContext.IsPlatformUser,
            isTenantUser = _currentUserContext.IsTenantUser,
            companyIds = _currentUserContext.CompanyIds,
            branchIds = _currentUserContext.BranchIds
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(
                request.Username,
                request.Email,
                request.Password);

            return Ok(new
            {
                status = "PASS",
                message = "User created successfully.",
                userId = user.Id,
                username = user.Username,
                email = user.Email,
                tenantId = user.TenantId,
                isActive = user.IsActive
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }
}