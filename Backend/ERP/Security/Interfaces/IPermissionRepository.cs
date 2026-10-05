using Security.Core.Models;

namespace Security.Interfaces;

public interface IPermissionRepository
{
    Task<Permission?> GetByCodeAsync(
        string code);

    Task AddAsync(
        Permission permission);

    Task SaveChangesAsync();
}