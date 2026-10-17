using System;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;
using Security.Services;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : BaseApiController
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
    public async Task<IActionResult> TestCompanyAccess(Guid companyId)
    {
        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing.");
        }

        var canAccess = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        return SuccessResponse(new
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
        return SuccessResponse(new
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
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(
                request.Username,
                request.Email,
                request.Password);

            return CreatedResponse("", null, new
            {
                userId = user.Id,
                username = user.Username,
                email = user.Email,
                tenantId = user.TenantId,
                isActive = user.IsActive
            }, "User created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}