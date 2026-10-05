using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolePermissionController : ControllerBase
{
    private readonly IRolePermissionService _rolePermissionService;

    public RolePermissionController(
        IRolePermissionService rolePermissionService)
    {
        _rolePermissionService = rolePermissionService;
    }

    [HttpPost]
    public async Task<IActionResult> AssignPermissionToRole(
        [FromBody] AssignPermissionToRoleRequest request)
    {
        try
        {
            await _rolePermissionService
                .AssignPermissionToRoleAsync(
                    request.RoleId,
                    request.PermissionId);

            return Ok(new
            {
                status = "PASS",
                message = "Permission assigned to role successfully.",
                roleId = request.RoleId,
                permissionId = request.PermissionId
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