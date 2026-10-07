using Security.Core.Models;

namespace Security.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(
        Guid? tenantId,
        string username);

    Task<User?> GetByEmailAsync(
        string email);

    Task<List<string>> GetRolesAsync(
        Guid userId);

    Task<List<string>> GetPermissionsAsync(
        Guid userId);

    Task AddAsync(User user);

    Task SaveChangesAsync();
}