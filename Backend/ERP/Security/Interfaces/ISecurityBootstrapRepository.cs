using Security.Core.Models;

namespace Security.Interfaces;

public interface ISecurityBootstrapRepository
{
    Task<Role?> GetPlatformAdminRoleAsync();

    Task<User?> GetPlatformAdminUserAsync();

    Task AddRoleAsync(Role role);

    Task AddUserAsync(User user);

    Task AddUserRoleAsync(UserRole userRole);

    Task SaveChangesAsync();
}