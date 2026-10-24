using System;
using System.Linq;
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
                // Tenant Admin can only see their tenant's roles and shared tenant system roles, strictly excluding platform-only roles
                query = query.Where(r => r.TenantId == tenantId.Value ||
                                         (r.TenantId == null && !PlatformSecurityConstants.PlatformRoles.Contains(r.Name)));
            }

            var roles = await query
                .OrderBy(r => r.Name)
                .Select(r => new
                {
                    id = r.Id.ToString(),
                    name = r.Name,
                    description = r.Description,
                    isSystem = r.IsSystemRole,
                    isActive = r.IsActive,
                    tenantId = r.TenantId.HasValue ? r.TenantId.Value.ToString() : null,
                    usersCount = r.UserRoles.Count(),
                    permissionsCount = r.RolePermissions.Count(rp => rp.Permission.IsActive)
                })
                .ToListAsync();

            return SuccessResponse(roles);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        try
        {
            var role = await _dbContext.Roles
                .Include(r => r.UserRoles)
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            var isPlatformUser = _currentUserContext.IsPlatformUser;

            if (!isPlatformUser)
            {
                if (!tenantId.HasValue)
                {
                    return UnauthorizedResponse("Tenant identity is missing or invalid.");
                }

                // Enforce tenant ownership: Role must belong to this tenant, or be an allowed non-platform system role
                if (role.TenantId.HasValue && role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to view roles belonging to another tenant.");
                }

                if (!role.TenantId.HasValue && PlatformSecurityConstants.IsPlatformRole(role.Name))
                {
                    return ForbiddenResponse("Tenant administrators cannot view platform-only roles.");
                }
            }

            return SuccessResponse(new
            {
                id = role.Id.ToString(),
                name = role.Name,
                description = role.Description,
                isSystem = role.IsSystemRole,
                isActive = role.IsActive,
                tenantId = role.TenantId.HasValue ? role.TenantId.Value.ToString() : null,
                usersCount = role.UserRoles.Count(),
                permissions = role.RolePermissions
                    .Where(rp => rp.Permission != null && rp.Permission.IsActive)
                    .Select(rp => new
                    {
                        id = rp.Permission.Id,
                        code = rp.Permission.Code,
                        name = rp.Permission.Name,
                        description = rp.Permission.Description,
                        module = rp.Permission.Module
                    })
                    .OrderBy(p => p.module)
                    .ThenBy(p => p.code)
                    .ToList()
            });
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
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequestResponse("Role name is required.");
            }

            var trimmedName = request.Name.Trim();

            if (PlatformSecurityConstants.IsPlatformRole(trimmedName) ||
                trimmedName.Equals("Tenant Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequestResponse("Cannot create a role with a reserved platform or system role name.");
            }

            var role = await _roleService.CreateRoleAsync(
                trimmedName,
                request.Description?.Trim() ?? string.Empty);

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

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequestResponse("Role name is required.");
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            if (role.IsSystemRole)
            {
                return BadRequestResponse("Protected system roles cannot be modified.");
            }

            if (PlatformSecurityConstants.IsPlatformRole(role.Name))
            {
                return BadRequestResponse("Platform roles cannot be modified.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (!role.TenantId.HasValue || role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to modify roles belonging to another tenant.");
                }
            }

            var trimmedName = request.Name.Trim();
            if (PlatformSecurityConstants.IsPlatformRole(trimmedName) ||
                trimmedName.Equals("Tenant Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequestResponse("Cannot rename role to a reserved platform or system role name.");
            }

            var duplicate = await _dbContext.Roles.AnyAsync(r =>
                r.Id != id &&
                r.TenantId == role.TenantId &&
                r.Name == trimmedName);

            if (duplicate)
            {
                return ConflictResponse("Another role with this name already exists in this scope.");
            }

            role.Name = trimmedName;
            role.Description = request.Description?.Trim() ?? string.Empty;

            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                id = role.Id.ToString(),
                name = role.Name,
                description = role.Description,
                tenantId = role.TenantId,
                isSystemRole = role.IsSystemRole,
                isActive = role.IsActive
            }, "Role updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/toggle-status")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> ToggleRoleStatus(Guid id)
    {
        try
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            if (role.IsSystemRole || PlatformSecurityConstants.IsPlatformRole(role.Name))
            {
                return BadRequestResponse("Protected system and platform roles cannot be deactivated.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (!role.TenantId.HasValue || role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to modify roles belonging to another tenant.");
                }
            }

            role.IsActive = !role.IsActive;
            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                id = role.Id.ToString(),
                name = role.Name,
                isActive = role.IsActive
            }, $"Role {(role.IsActive ? "activated" : "deactivated")} successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
