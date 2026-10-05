using Security.Core.Models;

namespace Security.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(
        Guid? tenantId,
        string name);

    Task AddAsync(Role role);

    Task SaveChangesAsync();
}