using System;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Application.DTOs;
using Security.Common;
using Security.Infrastructure.Persistence;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserRoleController : BaseApiController
{
    private readonly IUserRoleService _userRoleService;
    private readonly SecurityDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public UserRoleController(
        IUserRoleService userRoleService,
        SecurityDbContext dbContext,
        ICurrentUserContext currentUserContext)
    {
        _userRoleService = userRoleService;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> AssignRoleToUser([FromBody] AssignRoleToUserRequest request)
    {
        try
        {
            if (request == null || request.UserId == Guid.Empty || request.RoleId == Guid.Empty)
            {
                return BadRequestResponse("Valid UserId and RoleId are required.");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFoundResponse("User not found.");
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (user.TenantId != tenantId.Value)
                {
                    return ForbiddenResponse("Cannot assign roles to a user belonging to another tenant.");
                }

                if (PlatformSecurityConstants.IsPlatformRole(role.Name))
                {
                    return ForbiddenResponse("Tenant administrators cannot assign platform-only roles.");
                }

                if (role.TenantId.HasValue && role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("Cannot assign a role belonging to another tenant.");
                }
            }

            await _userRoleService.AssignRoleToUserAsync(
                request.UserId,
                request.RoleId);

            return SuccessResponse(new
            {
                userId = request.UserId,
                roleId = request.RoleId,
                roleName = role.Name
            }, "Role assigned to user successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> RemoveRoleFromUser([FromBody] AssignRoleToUserRequest request)
    {
        try
        {
            if (request == null || request.UserId == Guid.Empty || request.RoleId == Guid.Empty)
            {
                return BadRequestResponse("Valid UserId and RoleId are required.");
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFoundResponse("User not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && user.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("Cannot remove roles from a user belonging to another tenant.");
            }

            var userRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId);

            if (userRole == null)
            {
                return NotFoundResponse("Role is not assigned to this user.");
            }

            _dbContext.UserRoles.Remove(userRole);
            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                userId = request.UserId,
                roleId = request.RoleId
            }, "Role removed from user successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("remove")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public Task<IActionResult> RemoveRoleFromUserPost([FromBody] AssignRoleToUserRequest request)
    {
        return RemoveRoleFromUser(request);
    }
}
