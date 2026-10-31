using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Application.DTOs;
using Security.Common;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolePermissionController : BaseApiController
{
    private readonly IRolePermissionService _rolePermissionService;
    private readonly SecurityDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public RolePermissionController(
        IRolePermissionService rolePermissionService,
        SecurityDbContext dbContext,
        ICurrentUserContext currentUserContext)
    {
        _rolePermissionService = rolePermissionService;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("{roleId:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetRolePermissions(Guid roleId)
    {
        try
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (role.TenantId.HasValue && role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to view roles belonging to another tenant.");
                }

                if (!role.TenantId.HasValue && PlatformSecurityConstants.IsPlatformRole(role.Name))
                {
                    return ForbiddenResponse("Tenant administrators cannot view platform-only roles.");
                }
            }

            var permissions = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.Permission.IsActive)
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
                .ToListAsync();

            return SuccessResponse(permissions);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> AssignPermissionToRole([FromBody] AssignPermissionToRoleRequest request)
    {
        try
        {
            if (request == null || request.RoleId == Guid.Empty || request.PermissionId == Guid.Empty)
            {
                return BadRequestResponse("Valid RoleId and PermissionId are required.");
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            if (role.IsSystemRole || PlatformSecurityConstants.IsPlatformRole(role.Name))
            {
                return BadRequestResponse("Cannot modify permissions of protected system or platform roles.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (!role.TenantId.HasValue || role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to modify roles belonging to another tenant.");
                }

                var permission = await _dbContext.Permissions.FirstOrDefaultAsync(p => p.Id == request.PermissionId);
                if (permission == null)
                {
                    return NotFoundResponse("Permission not found.");
                }

                if (PlatformSecurityConstants.IsPlatformOnlyPermission(permission.Module, permission.Code))
                {
                    return ForbiddenResponse("Tenant administrators cannot assign platform-only permissions.");
                }
            }

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

    [HttpDelete]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> RemovePermissionFromRole([FromBody] AssignPermissionToRoleRequest request)
    {
        try
        {
            if (request == null || request.RoleId == Guid.Empty || request.PermissionId == Guid.Empty)
            {
                return BadRequestResponse("Valid RoleId and PermissionId are required.");
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            if (role.IsSystemRole || PlatformSecurityConstants.IsPlatformRole(role.Name))
            {
                return BadRequestResponse("Cannot modify permissions of protected system or platform roles.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (!role.TenantId.HasValue || role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to modify roles belonging to another tenant.");
                }
            }

            var rolePermission = await _dbContext.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == request.RoleId && rp.PermissionId == request.PermissionId);

            if (rolePermission == null)
            {
                return NotFoundResponse("Permission is not assigned to this role.");
            }

            _dbContext.RolePermissions.Remove(rolePermission);
            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                roleId = request.RoleId,
                permissionId = request.PermissionId
            }, "Permission removed from role successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("remove")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public Task<IActionResult> RemovePermissionFromRolePost([FromBody] AssignPermissionToRoleRequest request)
    {
        return RemovePermissionFromRole(request);
    }

    [HttpPost("batch")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> BatchAssignPermissions([FromBody] AssignRolePermissionsBatchRequest request)
    {
        try
        {
            if (request == null || request.RoleId == Guid.Empty)
            {
                return BadRequestResponse("Valid RoleId is required.");
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId);
            if (role == null)
            {
                return NotFoundResponse("Role not found.");
            }

            if (role.IsSystemRole || PlatformSecurityConstants.IsPlatformRole(role.Name))
            {
                return BadRequestResponse("Cannot modify permissions of protected system or platform roles.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue)
            {
                if (!role.TenantId.HasValue || role.TenantId.Value != tenantId.Value)
                {
                    return ForbiddenResponse("You do not have permission to modify roles belonging to another tenant.");
                }
            }

            // Resolve target permissions from PermissionIds and/or PermissionCodes
            var targetPermissionIds = new HashSet<Guid>();
            var rawCodesToResolve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (request.PermissionIds != null && request.PermissionIds.Count > 0)
            {
                foreach (var idOrCode in request.PermissionIds)
                {
                    if (string.IsNullOrWhiteSpace(idOrCode)) continue;
                    if (Guid.TryParse(idOrCode.Trim(), out var guid))
                    {
                        targetPermissionIds.Add(guid);
                    }
                    else
                    {
                        rawCodesToResolve.Add(idOrCode.Trim());
                    }
                }
            }

            if (request.PermissionCodes != null && request.PermissionCodes.Count > 0)
            {
                foreach (var codeOrId in request.PermissionCodes)
                {
                    if (string.IsNullOrWhiteSpace(codeOrId)) continue;
                    if (Guid.TryParse(codeOrId.Trim(), out var guid))
                    {
                        targetPermissionIds.Add(guid);
                    }
                    else
                    {
                        rawCodesToResolve.Add(codeOrId.Trim());
                    }
                }
            }

            if (rawCodesToResolve.Count > 0)
            {
                var matched = await _dbContext.Permissions
                    .Where(p => p.IsActive && rawCodesToResolve.Contains(p.Code))
                    .Select(p => p.Id)
                    .ToListAsync();

                foreach (var pid in matched)
                {
                    targetPermissionIds.Add(pid);
                }
            }

            // Verify none of the resolved permissions are platform-only if requested by Tenant Admin
            if (tenantId.HasValue && targetPermissionIds.Count > 0)
            {
                var targetPermissions = await _dbContext.Permissions
                    .Where(p => targetPermissionIds.Contains(p.Id))
                    .ToListAsync();

                var unauthorized = targetPermissions
                    .Where(p => PlatformSecurityConstants.IsPlatformOnlyPermission(p.Module, p.Code))
                    .ToList();

                if (unauthorized.Count > 0)
                {
                    return ForbiddenResponse("Tenant administrators cannot assign platform-only permissions.");
                }
            }

            // Synchronize permissions applying delta: preserve unchanged, remove deselected, add newly selected
            var existingAssignments = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == request.RoleId)
                .ToListAsync();

            var existingIds = existingAssignments.Select(rp => rp.PermissionId).ToHashSet();

            // 1. Remove assignments no longer present in target
            var toRemove = existingAssignments.Where(rp => !targetPermissionIds.Contains(rp.PermissionId)).ToList();
            if (toRemove.Count > 0)
            {
                _dbContext.RolePermissions.RemoveRange(toRemove);
            }

            // 2. Add newly selected permissions not already assigned
            var toAdd = targetPermissionIds.Where(pid => !existingIds.Contains(pid)).ToList();
            foreach (var pid in toAdd)
            {
                _dbContext.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = request.RoleId,
                    PermissionId = pid
                });
            }

            if (toRemove.Count > 0 || toAdd.Count > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            // 3. Return freshly loaded persisted assignments
            var persistedPermissions = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == request.RoleId && rp.Permission.IsActive)
                .Select(rp => new
                {
                    id = rp.Permission.Id,
                    code = rp.Permission.Code,
                    name = rp.Permission.Name,
                    module = rp.Permission.Module
                })
                .OrderBy(p => p.module)
                .ThenBy(p => p.code)
                .ToListAsync();

            return SuccessResponse(new
            {
                roleId = request.RoleId,
                assignedCount = persistedPermissions.Count,
                permissions = persistedPermissions
            }, "Role permissions synchronized successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
