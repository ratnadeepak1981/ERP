namespace Security.Interfaces;

public interface IUserRoleService
{
    Task AssignRoleToUserAsync(
        Guid userId,
        Guid roleId);
}