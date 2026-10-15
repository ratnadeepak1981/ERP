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
public class RoleController : BaseApiController
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            var role = await _roleService.CreateRoleAsync(
                request.Name,
                request.Description);

            return CreatedResponse("", null, new
            {
                roleId = role.Id,
                name = role.Name,
                description = role.Description,
                tenantId = role.TenantId,
                isSystemRole = role.IsSystemRole,
                isActive = role.IsActive
            }, "Role created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}