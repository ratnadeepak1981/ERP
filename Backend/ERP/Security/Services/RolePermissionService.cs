using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class RolePermissionService
    : IRolePermissionService
{
    private readonly IRolePermissionRepository _repository;

    public RolePermissionService(
        IRolePermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task AssignPermissionToRoleAsync(
        Guid roleId,
        Guid permissionId)
    {
        var existing =
            await _repository.GetAsync(
                roleId,
                permissionId);

        if (existing != null)
        {
            throw new InvalidOperationException(
                "Permission is already assigned to this role.");
        }

        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId
        };

        await _repository.AddAsync(rolePermission);

        await _repository.SaveChangesAsync();
    }
}