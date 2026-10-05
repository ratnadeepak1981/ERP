using Security.Core.Models;

namespace Security.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(
        Guid? tenantId,
        string username);

    Task AddAsync(
        User user);

    Task SaveChangesAsync();
}