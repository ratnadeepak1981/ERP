using Security.Core.Models;

namespace Security.Interfaces;

public interface IRolePermissionRepository
{
    Task<RolePermission?> GetAsync(
        Guid roleId,
        Guid permissionId);

    Task AddAsync(
        RolePermission rolePermission);

    Task SaveChangesAsync();
}