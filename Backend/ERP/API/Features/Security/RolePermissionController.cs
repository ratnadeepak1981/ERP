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
public class RolePermissionController : BaseApiController
{
    private readonly IRolePermissionService _rolePermissionService;

    public RolePermissionController(IRolePermissionService rolePermissionService)
    {
        _rolePermissionService = rolePermissionService;
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> AssignPermissionToRole([FromBody] AssignPermissionToRoleRequest request)
    {
        try
        {
            await _rolePermissionService.AssignPermissionToRoleAsync(
                request.RoleId,
                request.PermissionId);

            return SuccessResponse(new
            {
                roleId = request.RoleId,
                permissionId = request.PermissionId
            }, "Permission assigned to role successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}