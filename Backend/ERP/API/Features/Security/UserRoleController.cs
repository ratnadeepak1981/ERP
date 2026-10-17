using System;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserRoleController : BaseApiController
{
    private readonly IUserRoleService _userRoleService;

    public UserRoleController(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> AssignRoleToUser([FromBody] AssignRoleToUserRequest request)
    {
        try
        {
            await _userRoleService.AssignRoleToUserAsync(
                request.UserId,
                request.RoleId);

            return SuccessResponse(new
            {
                userId = request.UserId,
                roleId = request.RoleId
            }, "Role assigned to user successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}