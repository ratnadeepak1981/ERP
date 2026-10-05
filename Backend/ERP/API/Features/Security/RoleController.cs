using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RoleController(
        IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleRequest request)
    {
        try
        {
            var role = await _roleService.CreateRoleAsync(
                request.Name,
                request.Description);

            return Ok(new
            {
                status = "PASS",
                message = "Role created successfully.",
                roleId = role.Id,
                name = role.Name,
                description = role.Description,
                tenantId = role.TenantId,
                isSystemRole = role.IsSystemRole,
                isActive = role.IsActive
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