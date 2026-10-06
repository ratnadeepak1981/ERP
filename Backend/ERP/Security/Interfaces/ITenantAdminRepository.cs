using Security.Core.Models;

namespace Security.Interfaces;

public interface ITenantAdminRepository
{
    Task<User?> GetUserByUsernameAsync(
        string username);

    Task<Role?> GetTenantAdminRoleAsync(
        Guid tenantId);

    Task AddUserAsync(
        User user);

    Task AddRoleAsync(
        Role role);

    Task AddUserRoleAsync(
        UserRole userRole);

    Task SaveChangesAsync();
}