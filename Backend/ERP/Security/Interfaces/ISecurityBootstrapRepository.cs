using Security.Core.Models;

namespace Security.Interfaces;

public interface ISecurityBootstrapRepository
{
    // =========================
    // Role Lookups
    // =========================

    Task<Role?> GetPlatformAdminRoleAsync();

    Task<Role?> GetTenantAdminRoleAsync();

    Task<Role?> GetSystemRoleAsync(string name);

    // =========================
    // User Lookups
    // =========================

    Task<User?> GetPlatformAdminUserAsync();

    Task<User?> GetPlatformUserAsync(
        string username);

    Task<User?> GetTenantAdminUserAsync(
        Guid tenantId,
        string username);

    // =========================
    // RBAC Lookups
    // =========================

    Task<Permission?> GetPermissionByCodeAsync(
        string code);

    Task<RolePermission?> GetRolePermissionAsync(
        Guid roleId,
        Guid permissionId);

    Task<UserRole?> GetUserRoleAsync(
        Guid userId,
        Guid roleId);

    // =========================
    // Inserts
    // =========================

    Task AddRoleAsync(Role role);

    Task AddUserAsync(User user);

    Task AddUserRoleAsync(UserRole userRole);

    Task AddPermissionAsync(Permission permission);

    Task AddRolePermissionAsync(
        RolePermission rolePermission);

    // =========================
    // Persistence
    // =========================

    Task SaveChangesAsync();
}