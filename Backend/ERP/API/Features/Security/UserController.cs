using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Security.Application.DTOs;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Interfaces;
using Security.Services;

namespace API.Features.Security;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : BaseApiController
{
    private readonly IUserService _userService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;
    private readonly SecurityDbContext _dbContext;

    public UserController(
        IUserService userService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        SecurityDbContext dbContext)
    {
        _userService = userService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _dbContext = dbContext;
    }

    [HttpGet("access-test/company/{companyId:guid}")]
    public async Task<IActionResult> TestCompanyAccess(Guid companyId)
    {
        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing.");
        }

        var canAccess = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        return SuccessResponse(new
        {
            status = canAccess ? "PASS" : "DENY",
            userId = _currentUserContext.UserId,
            tenantId = tenantId.Value,
            companyId,
            canAccess
        });
    }

    [HttpGet("context")]
    public IActionResult GetContext()
    {
        return SuccessResponse(new
        {
            userId = _currentUserContext.UserId,
            tenantId = _currentUserContext.TenantId,
            isPlatformUser = _currentUserContext.IsPlatformUser,
            isTenantUser = _currentUserContext.IsTenantUser,
            companyIds = _currentUserContext.CompanyIds,
            branchIds = _currentUserContext.BranchIds
        });
    }

    [HttpGet]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var tenantId = _currentUserContext.TenantId;
            var query = _dbContext.Users.AsQueryable();

            if (tenantId.HasValue)
            {
                query = query.Where(u => u.TenantId == tenantId.Value);
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    id = u.Id.ToString(),
                    tenantId = u.TenantId.HasValue ? u.TenantId.Value.ToString() : null,
                    username = u.Username,
                    email = u.Email,
                    fullName = u.Username,
                    accountStatus = u.IsActive ? "Active" : "Suspended",
                    roles = u.UserRoles.Select(ur => new
                    {
                        roleId = ur.RoleId.ToString(),
                        roleName = ur.Role.Name,
                        scopes = ur.Scopes.Where(s => s.IsActive).Select(s => new
                        {
                            scopeId = s.Id.ToString(),
                            companyId = s.CompanyId.HasValue ? s.CompanyId.Value.ToString() : null,
                            branchId = s.BranchId.HasValue ? s.BranchId.Value.ToString() : null
                        }).ToList()
                    }).ToList(),
                    createdAt = u.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return SuccessResponse(users);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Platform Admin, Security Manager")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        return await HandleCreateUserInternal(request);
    }

    [HttpPost("tenant-user")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> CreateTenantUser([FromBody] CreateUserRequest request)
    {
        return await HandleCreateUserInternal(request);
    }

    private async Task<IActionResult> HandleCreateUserInternal(CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequestResponse("Email is required.");
            }

            var username = !string.IsNullOrWhiteSpace(request.Username)
                ? request.Username.Trim()
                : request.Email.Split('@')[0];

            var password = !string.IsNullOrWhiteSpace(request.Password)
                ? request.Password
                : "Password@123";

            var user = await _userService.CreateUserAsync(
                username,
                request.Email.Trim(),
                password);

            // Link Role to user
            UserRole? userRole = null;
            if (request.RoleId.HasValue && request.RoleId.Value != Guid.Empty)
            {
                userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = request.RoleId.Value
                };
                await _dbContext.UserRoles.AddAsync(userRole);
            }
            else
            {
                // Assign standard default Tenant User role if present
                var defaultRole = await _dbContext.Roles
                    .FirstOrDefaultAsync(r => r.Name == "Tenant User" || r.Name == "Tenant Staff");
                if (defaultRole != null)
                {
                    userRole = new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        RoleId = defaultRole.Id
                    };
                    await _dbContext.UserRoles.AddAsync(userRole);
                }
            }

            // Link User Scope (Company & Branch) to user role
            UserRoleScope? userRoleScope = null;
            if (userRole != null && request.CompanyId.HasValue && request.CompanyId.Value != Guid.Empty)
            {
                userRoleScope = new UserRoleScope
                {
                    Id = Guid.NewGuid(),
                    UserRoleId = userRole.Id,
                    CompanyId = request.CompanyId.Value,
                    BranchId = request.BranchId.HasValue && request.BranchId.Value != Guid.Empty ? request.BranchId.Value : null,
                    IsActive = true
                };
                await _dbContext.UserRoleScopes.AddAsync(userRoleScope);
            }

            await _dbContext.SaveChangesAsync();

            string roleName = "Authorized User";
            if (userRole != null)
            {
                var roleObj = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == userRole.RoleId);
                if (roleObj != null)
                {
                    roleName = roleObj.Name;
                }
            }

            return CreatedResponse("", null, new
            {
                userId = user.Id,
                username = user.Username,
                email = user.Email,
                tenantId = user.TenantId,
                roleId = userRole?.RoleId,
                roleName = roleName,
                companyId = request.CompanyId,
                branchId = request.BranchId,
                isActive = user.IsActive
            }, "User created and linked with role and user scope successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("scopes")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetScopes()
    {
        try
        {
            var tenantId = _currentUserContext.TenantId;
            var query = _dbContext.UserRoleScopes
                .Include(s => s.UserRole)
                    .ThenInclude(ur => ur.User)
                .Where(s => s.IsActive)
                .AsQueryable();

            if (tenantId.HasValue)
            {
                query = query.Where(s => s.UserRole.User.TenantId == tenantId.Value);
            }

            var scopes = await query
                .Select(s => new
                {
                    id = s.Id.ToString(),
                    userId = s.UserRole.UserId.ToString(),
                    userName = s.UserRole.User.Username,
                    userEmail = s.UserRole.User.Email,
                    companyId = s.CompanyId.HasValue ? s.CompanyId.Value.ToString() : null,
                    branchId = s.BranchId.HasValue ? s.BranchId.Value.ToString() : null,
                    isActive = s.IsActive,
                    grantedAt = s.CreatedAt.ToString("o")
                })
                .ToListAsync();

            return SuccessResponse(scopes);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("scope")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> AssignScope([FromBody] AssignUserScopeRequest request)
    {
        try
        {
            var userRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == request.UserId);

            if (userRole == null)
            {
                var roleId = request.RoleId ?? (await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Tenant User" || r.Name == "Tenant Staff"))?.Id;
                if (!roleId.HasValue)
                {
                    return BadRequestResponse("User does not have an active role assigned.");
                }

                userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    RoleId = roleId.Value
                };
                await _dbContext.UserRoles.AddAsync(userRole);
                await _dbContext.SaveChangesAsync();
            }

            var scope = new UserRoleScope
            {
                Id = Guid.NewGuid(),
                UserRoleId = userRole.Id,
                CompanyId = request.CompanyId != Guid.Empty ? request.CompanyId : null,
                BranchId = request.BranchId.HasValue && request.BranchId.Value != Guid.Empty ? request.BranchId.Value : null,
                IsActive = true
            };

            await _dbContext.UserRoleScopes.AddAsync(scope);
            await _dbContext.SaveChangesAsync();

            return CreatedResponse("", null, new
            {
                scopeId = scope.Id,
                userId = request.UserId,
                companyId = scope.CompanyId,
                branchId = scope.BranchId,
                isActive = scope.IsActive
            }, "User scope assigned successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{userId:guid}/toggle-status")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> ToggleUserStatus(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFoundResponse("User not found.");
            }

            user.IsActive = !user.IsActive;
            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                userId = user.Id,
                isActive = user.IsActive,
                accountStatus = user.IsActive ? "Active" : "Suspended"
            }, "User status updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}