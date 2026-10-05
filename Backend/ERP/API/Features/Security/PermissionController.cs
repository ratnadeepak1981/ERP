using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Application.DTOs;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionController(
        IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePermission(
        [FromBody] CreatePermissionRequest request)
    {
        try
        {
            var permission =
                await _permissionService.CreatePermissionAsync(
                    request.Code,
                    request.Name,
                    request.Description,
                    request.Module);

            return Ok(new
            {
                status = "PASS",
                message = "Permission created successfully.",
                permissionId = permission.Id,
                code = permission.Code,
                name = permission.Name,
                description = permission.Description,
                module = permission.Module,
                isActive = permission.IsActive
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