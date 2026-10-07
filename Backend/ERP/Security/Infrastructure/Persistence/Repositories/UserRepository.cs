using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SecurityDbContext _context;

    public UserRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByUsernameAsync(
        Guid? tenantId,
        string username)
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Username == username);
    }

    public Task<User?> GetByEmailAsync(
        string email)
    {
        return _context.Users
            .FirstOrDefaultAsync(x =>
                x.Email == email);
    }

    public Task<List<string>> GetRolesAsync(
        Guid userId)
    {
        return _context.UserRoles
            .Where(x => x.UserId == userId)
            .Include(x => x.Role)
            .Where(x => x.Role.IsActive)
            .Select(x => x.Role.Name)
            .Distinct()
            .ToListAsync();
    }

    public Task<List<string>> GetPermissionsAsync(
        Guid userId)
    {
        return _context.UserRoles
            .Where(x => x.UserId == userId)
            .Include(x => x.Role)
                .ThenInclude(x => x.RolePermissions)
                    .ThenInclude(x => x.Permission)
            .SelectMany(x => x.Role.RolePermissions)
            .Where(x => x.Permission.IsActive)
            .Select(x => x.Permission.Code)
            .Distinct()
            .ToListAsync();
    }

    public async Task AddAsync(
        User user)
    {
        await _context.Users.AddAsync(user);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}