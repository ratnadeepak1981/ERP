using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserRoleController : ControllerBase
{
    private readonly IUserRoleService _userRoleService;

    public UserRoleController(
        IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    [HttpPost]
    public async Task<IActionResult> AssignRoleToUser(
        [FromBody] AssignRoleToUserRequest request)
    {
        try
        {
            await _userRoleService
                .AssignRoleToUserAsync(
                    request.UserId,
                    request.RoleId);

            return Ok(new
            {
                status = "PASS",
                message = "Role assigned to user successfully.",
                userId = request.UserId,
                roleId = request.RoleId
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