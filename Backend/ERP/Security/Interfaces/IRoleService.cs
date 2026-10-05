using Security.Core.Models;

namespace Security.Interfaces;

public interface IRoleService
{
    Task<Role> CreateRoleAsync(
        string name,
        string description);
}