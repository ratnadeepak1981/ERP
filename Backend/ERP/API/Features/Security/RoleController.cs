using System;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Application.DTOs;
using Security.Infrastructure.Persistence;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : BaseApiController
{
    private readonly IRoleService _roleService;
    private readonly SecurityDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public RoleController(
        IRoleService roleService,
        SecurityDbContext dbContext,
        ICurrentUserContext currentUserContext)
    {
        _roleService = roleService;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetRoles()
    {
        try
        {
            var tenantId = _currentUserContext.TenantId;
            var query = _dbContext.Roles.Where(r => r.IsActive);

            if (tenantId.HasValue)
            {
                query = query.Where(r => r.TenantId == tenantId.Value || r.TenantId == null);
            }

            var roles = await query
                .OrderBy(r => r.Name)
                .Select(r => new
                {
                    id = r.Id.ToString(),
                    name = r.Name,
                    description = r.Description,
                    isSystem = r.IsSystemRole,
                    tenantId = r.TenantId.HasValue ? r.TenantId.Value.ToString() : null,
                    usersCount = r.UserRoles.Count()
                })
                .ToListAsync();

            return SuccessResponse(roles);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
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