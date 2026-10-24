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
public class PermissionController : BaseApiController
{
    private readonly IPermissionService _permissionService;
    private readonly SecurityDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public PermissionController(
        IPermissionService permissionService,
        SecurityDbContext dbContext,
        ICurrentUserContext currentUserContext)
    {
        _permissionService = permissionService;
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetPermissions([FromQuery] string? module = null)
    {
        try
        {
            var query = _dbContext.Permissions.Where(p => p.IsActive);

            // Tenant administrators cannot view platform-only permissions (Tenant, Subscription)
            if (_currentUserContext.TenantId.HasValue)
            {
                query = query.Where(p =>
                    p.Module != "Tenant" &&
                    p.Module != "Subscription" &&
                    !p.Code.StartsWith("TENANT.") &&
                    !p.Code.StartsWith("SUBSCRIPTION."));
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                var trimmedModule = module.Trim();
                query = query.Where(p => p.Module == trimmedModule);
            }

            var permissions = await query
                .OrderBy(p => p.Module)
                .ThenBy(p => p.Code)
                .Select(p => new
                {
                    id = p.Id,
                    code = p.Code,
                    name = p.Name,
                    description = p.Description,
                    module = p.Module,
                    isActive = p.IsActive
                })
                .ToListAsync();

            return SuccessResponse(permissions);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
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
