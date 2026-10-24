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
using Security.Services;
using Domain.Infrastructure.Persistence;

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
    private readonly DomainDbContext _domainDbContext;

    public UserController(
        IUserService userService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService,
        SecurityDbContext dbContext,
        DomainDbContext domainDbContext)
    {
        _userService = userService;
        _currentUserContext = currentUserContext;
        _userAccessService = userAccessService;
        _dbContext = dbContext;
        _domainDbContext = domainDbContext;
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

    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetUserById(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Scopes)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFoundResponse("User not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && user.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("You do not have permission to view users belonging to another tenant.");
            }

            return SuccessResponse(new
            {
                id = user.Id.ToString(),
                tenantId = user.TenantId.HasValue ? user.TenantId.Value.ToString() : null,
                username = user.Username,
                email = user.Email,
                fullName = user.Username,
                accountStatus = user.IsActive ? "Active" : "Suspended",
                roles = user.UserRoles.Select(ur => new
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
                createdAt = user.CreatedAt.ToString("o")
            });
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
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequestResponse("Email is required.");
            }

            var username = !string.IsNullOrWhiteSpace(request.Username)
                ? request.Username.Trim()
                : request.Email.Split('@')[0];

            var password = !string.IsNullOrWhiteSpace(request.Password)
                ? request.Password
                : "Password@123";

            var tenantId = _currentUserContext.TenantId;

            // Validate requested role before creating user
            Role? roleObj = null;
            if (request.RoleId.HasValue && request.RoleId.Value != Guid.Empty)
            {
                roleObj = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId.Value);
                if (roleObj == null)
                {
                    return NotFoundResponse("Assigned role not found.");
                }

                if (tenantId.HasValue)
                {
                    if (PlatformSecurityConstants.IsPlatformRole(roleObj.Name) ||
                        (roleObj.TenantId.HasValue && roleObj.TenantId.Value != tenantId.Value))
                    {
                        return ForbiddenResponse("Tenant administrators cannot assign platform-only or external tenant roles.");
                    }
                }
            }

            var user = await _userService.CreateUserAsync(
                username,
                request.Email.Trim(),
                password);

            // Link Role to user
            UserRole? userRole = null;
            if (roleObj != null)
            {
                userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = roleObj.Id
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
                    roleObj = defaultRole;
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

            string roleName = roleObj?.Name ?? "Authorized User";

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

    [HttpPut("{userId:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> UpdateUser(Guid userId, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _dbContext.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFoundResponse("User not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && user.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("You do not have permission to modify users belonging to another tenant.");
            }

            if (!string.IsNullOrWhiteSpace(request?.Username))
            {
                user.Username = request.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request?.Email))
            {
                user.Email = request.Email.Trim().ToLowerInvariant();
            }

            if (request?.IsActive.HasValue == true)
            {
                user.IsActive = request.IsActive.Value;
            }

            if (request?.RoleId.HasValue == true && request.RoleId.Value != Guid.Empty)
            {
                var roleObj = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId.Value);
                if (roleObj == null)
                {
                    return NotFoundResponse("Role not found.");
                }

                if (tenantId.HasValue)
                {
                    if (PlatformSecurityConstants.IsPlatformRole(roleObj.Name) ||
                        (roleObj.TenantId.HasValue && roleObj.TenantId.Value != tenantId.Value))
                    {
                        return ForbiddenResponse("Tenant administrators cannot assign platform-only or external tenant roles.");
                    }
                }

                var existingUserRole = user.UserRoles.FirstOrDefault();
                if (existingUserRole != null)
                {
                    existingUserRole.RoleId = roleObj.Id;
                }
                else
                {
                    _dbContext.UserRoles.Add(new UserRole
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        RoleId = roleObj.Id
                    });
                }
            }

            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                userId = user.Id,
                username = user.Username,
                email = user.Email,
                isActive = user.IsActive
            }, "User updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("scopes")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> GetScopes(
        [FromQuery] Guid? userId = null,
        [FromQuery] bool includeInactive = true)
    {
        try
        {
            var tenantId = _currentUserContext.TenantId;
            var query = _dbContext.UserRoleScopes
                .Include(s => s.UserRole)
                    .ThenInclude(ur => ur.User)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(s => s.IsActive);
            }

            if (tenantId.HasValue)
            {
                query = query.Where(s => s.UserRole.User.TenantId == tenantId.Value);
            }

            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                query = query.Where(s => s.UserRole.UserId == userId.Value);
            }

            var dbScopes = await query
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var companyIds = dbScopes
                .Where(s => s.CompanyId.HasValue)
                .Select(s => s.CompanyId!.Value)
                .Distinct()
                .ToList();

            var branchIds = dbScopes
                .Where(s => s.BranchId.HasValue)
                .Select(s => s.BranchId!.Value)
                .Distinct()
                .ToList();

            var companies = await _domainDbContext.Companies
                .Where(c => companyIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var branches = await _domainDbContext.Branches
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var scopes = dbScopes.Select(s =>
            {
                var comp = s.CompanyId.HasValue && companies.TryGetValue(s.CompanyId.Value, out var c) ? c : null;
                var branch = s.BranchId.HasValue && branches.TryGetValue(s.BranchId.Value, out var b) ? b : null;
                var isAllBranches = !s.BranchId.HasValue;

                return new
                {
                    id = s.Id.ToString(),
                    userId = s.UserRole.UserId.ToString(),
                    userName = s.UserRole.User.Username,
                    userEmail = s.UserRole.User.Email,
                    companyId = s.CompanyId.HasValue ? s.CompanyId.Value.ToString() : null,
                    companyName = comp?.Name ?? (s.CompanyId.HasValue ? "Operating Company" : "All Companies"),
                    companyCode = comp?.Code ?? "",
                    branchId = s.BranchId.HasValue ? s.BranchId.Value.ToString() : null,
                    branchName = isAllBranches ? "All Facilities" : (branch?.Name ?? "Operating Branch"),
                    branchCode = branch?.Code ?? "",
                    isAllBranches = isAllBranches,
                    scopeLevel = isAllBranches ? "Company-wide" : "Branch-specific",
                    isActive = s.IsActive,
                    status = s.IsActive ? "Authorized" : "Revoked",
                    grantedAt = s.CreatedAt.ToString("o")
                };
            }).ToList();

            return SuccessResponse(scopes);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{userId:guid}/scopes")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public Task<IActionResult> GetUserScopes(Guid userId, [FromQuery] bool includeInactive = true)
    {
        return GetScopes(userId, includeInactive);
    }

    [HttpPost("scope")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> AssignScope([FromBody] AssignUserScopeRequest request)
    {
        try
        {
            if (request == null || request.UserId == Guid.Empty)
            {
                return BadRequestResponse("Valid UserId is required.");
            }

            if (request.CompanyId == Guid.Empty)
            {
                return BadRequestResponse("Valid CompanyId is required.");
            }

            var targetUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (targetUser == null)
            {
                return NotFoundResponse("User not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && targetUser.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("Cannot assign scope to a user belonging to another tenant.");
            }

            var company = await _domainDbContext.Companies.FirstOrDefaultAsync(c => c.Id == request.CompanyId);
            if (company == null || (tenantId.HasValue && company.TenantId != tenantId.Value))
            {
                return NotFoundResponse("Operating company not found or belongs to another tenant.");
            }

            global::Domain.Features.MasterData.Branch.Branch? branch = null;
            if (!request.IsAllBranches && request.BranchId.HasValue && request.BranchId.Value != Guid.Empty)
            {
                branch = await _domainDbContext.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId.Value && b.CompanyId == company.Id);
                if (branch == null)
                {
                    return NotFoundResponse("Operating branch not found under the specified company.");
                }
            }

            var userRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == request.UserId);

            if (userRole == null)
            {
                Role? roleToAssign = null;
                if (request.RoleId.HasValue && request.RoleId.Value != Guid.Empty)
                {
                    roleToAssign = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId.Value);
                }
                else
                {
                    roleToAssign = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Tenant User" || r.Name == "Tenant Staff");
                }

                if (roleToAssign == null)
                {
                    return BadRequestResponse("User does not have an active role assigned, and default role is unavailable.");
                }

                if (tenantId.HasValue && (PlatformSecurityConstants.IsPlatformRole(roleToAssign.Name) ||
                    (roleToAssign.TenantId.HasValue && roleToAssign.TenantId.Value != tenantId.Value)))
                {
                    return ForbiddenResponse("Cannot assign platform-only or external tenant role.");
                }

                userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    RoleId = roleToAssign.Id
                };
                await _dbContext.UserRoles.AddAsync(userRole);
                await _dbContext.SaveChangesAsync();
            }

            Guid targetCompanyId = request.CompanyId;
            Guid? targetBranchId = request.IsAllBranches ? null : (request.BranchId.HasValue && request.BranchId.Value != Guid.Empty ? request.BranchId.Value : null);

            var existingScope = await _dbContext.UserRoleScopes
                .FirstOrDefaultAsync(s => s.UserRoleId == userRole.Id &&
                                          s.CompanyId == targetCompanyId &&
                                          s.BranchId == targetBranchId);

            if (existingScope != null)
            {
                if (existingScope.IsActive)
                {
                    return ConflictResponse("This operating scope is already assigned to the user.");
                }

                existingScope.IsActive = true;
                await _dbContext.SaveChangesAsync();

                return SuccessResponse(new
                {
                    scopeId = existingScope.Id,
                    userId = request.UserId,
                    companyId = existingScope.CompanyId,
                    companyName = company.Name,
                    companyCode = company.Code,
                    branchId = existingScope.BranchId,
                    branchName = targetBranchId == null ? "All Facilities" : (branch?.Name ?? "Operating Branch"),
                    branchCode = branch?.Code ?? "",
                    isAllBranches = targetBranchId == null,
                    scopeLevel = targetBranchId == null ? "Company-wide" : "Branch-specific",
                    isActive = existingScope.IsActive,
                    status = "Authorized",
                    isReactivated = true
                }, "Operating scope reactivated successfully.");
            }

            var scope = new UserRoleScope
            {
                Id = Guid.NewGuid(),
                UserRoleId = userRole.Id,
                CompanyId = targetCompanyId,
                BranchId = targetBranchId,
                IsActive = true
            };

            await _dbContext.UserRoleScopes.AddAsync(scope);
            await _dbContext.SaveChangesAsync();

            return CreatedResponse("", null, new
            {
                scopeId = scope.Id,
                userId = request.UserId,
                companyId = scope.CompanyId,
                companyName = company.Name,
                companyCode = company.Code,
                branchId = scope.BranchId,
                branchName = targetBranchId == null ? "All Facilities" : (branch?.Name ?? "Operating Branch"),
                branchCode = branch?.Code ?? "",
                isAllBranches = targetBranchId == null,
                scopeLevel = targetBranchId == null ? "Company-wide" : "Branch-specific",
                isActive = scope.IsActive,
                status = "Authorized",
                isReactivated = false
            }, "Operating scope assigned successfully.");
        }
        catch (DbUpdateException dbEx)
        {
            if (dbEx.InnerException?.Message.Contains("IX_UserRoleScopes_UserRoleId_CompanyId_BranchId") == true ||
                dbEx.Message.Contains("IX_UserRoleScopes_UserRoleId_CompanyId_BranchId"))
            {
                return ConflictResponse("This operating scope is already assigned to the user.");
            }
            return HandleDomainException(dbEx);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("scope/{scopeId:guid}")]
    [Authorize(Roles = "Platform Admin, Security Manager, Tenant Admin")]
    public async Task<IActionResult> RevokeScope(Guid scopeId)
    {
        try
        {
            var scope = await _dbContext.UserRoleScopes
                .Include(s => s.UserRole)
                    .ThenInclude(ur => ur.User)
                .FirstOrDefaultAsync(s => s.Id == scopeId);

            if (scope == null)
            {
                return NotFoundResponse("User scope assignment not found.");
            }

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && scope.UserRole.User.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("You do not have permission to revoke scopes for another tenant.");
            }

            scope.IsActive = false;
            await _dbContext.SaveChangesAsync();

            return SuccessResponse(new
            {
                scopeId = scope.Id,
                isActive = scope.IsActive
            }, "User scope revoked successfully.");
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

            var tenantId = _currentUserContext.TenantId;
            if (tenantId.HasValue && user.TenantId != tenantId.Value)
            {
                return ForbiddenResponse("Cannot modify status of a user belonging to another tenant.");
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
