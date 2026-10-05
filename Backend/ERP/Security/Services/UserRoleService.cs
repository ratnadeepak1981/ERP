using Security.Core.Models;
using Security.Interfaces;

namespace Security.Services;

public class UserRoleService : IUserRoleService
{
    private readonly IUserRoleRepository _repository;

    public UserRoleService(
        IUserRoleRepository repository)
    {
        _repository = repository;
    }

    public async Task AssignRoleToUserAsync(
        Guid userId,
        Guid roleId)
    {
        var existing =
            await _repository.GetAsync(
                userId,
                roleId);

        if (existing != null)
        {
            throw new InvalidOperationException(
                "Role is already assigned to this user.");
        }

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId
        };

        await _repository.AddAsync(userRole);
        await _repository.SaveChangesAsync();
    }
}