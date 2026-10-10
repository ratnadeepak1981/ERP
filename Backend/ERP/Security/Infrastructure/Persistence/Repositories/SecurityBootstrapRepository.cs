using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class SecurityBootstrapRepository
    : ISecurityBootstrapRepository
{
    private readonly SecurityDbContext _context;

    public SecurityBootstrapRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    // =========================
    // Role Lookups
    // =========================

    public Task<Role?> GetPlatformAdminRoleAsync()
    {
        return _context.Roles
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Name == "Platform Admin");
    }

    public Task<Role?> GetTenantAdminRoleAsync()
    {
        return _context.Roles
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Name == "Tenant Admin");
    }

    public Task<Role?> GetSystemRoleAsync(
        string name)
    {
        return _context.Roles
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.IsSystemRole &&
                x.Name == name);
    }

    // =========================
    // User Lookups
    // =========================

    public Task<User?> GetPlatformAdminUserAsync()
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Username == "admin");
    }

    public Task<User?> GetPlatformUserAsync(
        string username)
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Username == username);
    }

    public Task<User?> GetTenantAdminUserAsync(
        Guid tenantId,
        string username)
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Username == username);
    }

    // =========================
    // RBAC Lookups
    // =========================

    public Task<Permission?> GetPermissionByCodeAsync(
        string code)
    {
        return _context.Permissions
            .FirstOrDefaultAsync(x =>
                x.Code == code);
    }

    public Task<RolePermission?> GetRolePermissionAsync(
        Guid roleId,
        Guid permissionId)
    {
        return _context.RolePermissions
            .FirstOrDefaultAsync(x =>
                x.RoleId == roleId &&
                x.PermissionId == permissionId);
    }

    public Task<UserRole?> GetUserRoleAsync(
        Guid userId,
        Guid roleId)
    {
        return _context.UserRoles
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.RoleId == roleId);
    }

    // =========================
    // Inserts
    // =========================

    public async Task AddRoleAsync(Role role)
    {
        await _context.Roles.AddAsync(role);
    }

    public async Task AddUserAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task AddUserRoleAsync(
        UserRole userRole)
    {
        await _context.UserRoles.AddAsync(userRole);
    }

    public async Task AddPermissionAsync(
        Permission permission)
    {
        await _context.Permissions.AddAsync(permission);
    }

    public async Task AddRolePermissionAsync(
        RolePermission rolePermission)
    {
        await _context.RolePermissions.AddAsync(
            rolePermission);
    }

    // =========================
    // Persistence
    // =========================

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}