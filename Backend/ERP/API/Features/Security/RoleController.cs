using System;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Application.DTOs;
using Security.Bootstrap.Roles;
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
    [RequirePermission("ROLE.VIEW")]
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
                    templateKey = r.TemplateKey,
                    templateVersion = r.TemplateVersion,
                    isTemplate = r.TemplateKey != null,
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
    [RequirePermission("ROLE.VIEW")]
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
                templateKey = role.TemplateKey,
                templateVersion = role.TemplateVersion,
                isTemplate = role.TemplateKey != null,
                usersCount = role.UserRoles.Count(),
                permissions = role.RolePermissions
                    .Where(rp => rp.Permission != null && rp.Permission.IsActive)
                    .Select(rp => new
                    {
                        id = rp.Permission.Id,
                        code = rp.Permission.Code,
                        name = rp.Permission.Name,
                        description = rp.Permission.Description,
                        module = rp.Permission.Module,
                        category = rp.Permission.Category,
                        operation = rp.Permission.Operation
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
    [RequirePermission("ROLE.CREATE")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequestResponse("Role name is required.");
            }

            var trimmedName = request.Name.Trim();

            if (IsReservedRoleName(trimmedName))
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
    [RequirePermission("ROLE.EDIT")]
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
            if (IsReservedRoleName(trimmedName) &&
                !string.Equals(trimmedName, role.Name, StringComparison.OrdinalIgnoreCase))
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
    [RequirePermission("ROLE.DELETE, ROLE.EDIT")]
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

            // Deactivating an active role requires ROLE.DELETE permission.
            // Reactivating an inactive role requires ROLE.EDIT permission.
            if (role.IsActive && !User.HasClaim("permission", "ROLE.DELETE"))
            {
                return ForbiddenResponse("Deactivating a role requires ROLE.DELETE permission.");
            }
            if (!role.IsActive && !User.HasClaim("permission", "ROLE.EDIT") && !User.HasClaim("permission", "ROLE.UPDATE"))
            {
                return ForbiddenResponse("Reactivating a role requires ROLE.EDIT permission.");
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

    private static bool IsReservedRoleName(string name)
    {
        if (PlatformSecurityConstants.IsPlatformRole(name) ||
            PlatformSecurityConstants.SystemTenantRoles.Contains(name))
        {
            return true;
        }

        return StandardRoleTemplates.All.Any(template =>
            string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
