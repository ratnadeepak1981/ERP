using Security.Core.Models;

namespace Security.Interfaces;

public interface IUserRoleRepository
{
    Task<UserRole?> GetAsync(
        Guid userId,
        Guid roleId);

    Task AddAsync(
        UserRole userRole);

    Task SaveChangesAsync();
}