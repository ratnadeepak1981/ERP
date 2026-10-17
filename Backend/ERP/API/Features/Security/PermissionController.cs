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
public class PermissionController : BaseApiController
{
    private readonly IPermissionService _permissionService;

    public PermissionController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        try
        {
            var permission = await _permissionService.CreatePermissionAsync(
                request.Code,
                request.Name,
                request.Description,
                request.Module);

            return CreatedResponse("", null, new
            {
                permissionId = permission.Id,
                code = permission.Code,
                name = permission.Name,
                description = permission.Description,
                module = permission.Module,
                isActive = permission.IsActive
            }, "Permission created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}