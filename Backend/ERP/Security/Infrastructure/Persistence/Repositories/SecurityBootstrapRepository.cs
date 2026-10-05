using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class SecurityBootstrapRepository  : ISecurityBootstrapRepository
{
    private readonly SecurityDbContext _context;

    public SecurityBootstrapRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    public Task<Role?> GetPlatformAdminRoleAsync()
    {
        return _context.Roles
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Name == "Platform Admin");
    }

    public Task<User?> GetPlatformAdminUserAsync()
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.TenantId == null &&
                x.Username == "admin");
    }

    public async Task AddRoleAsync(Role role)
    {
        await _context.Roles.AddAsync(role);
    }

    public async Task AddUserAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task AddUserRoleAsync(UserRole userRole)
    {
        await _context.UserRoles.AddAsync(userRole);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}