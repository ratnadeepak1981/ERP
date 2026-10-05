namespace Security.Interfaces;

public interface IRolePermissionService
{
    Task AssignPermissionToRoleAsync(
        Guid roleId,
        Guid permissionId);
}