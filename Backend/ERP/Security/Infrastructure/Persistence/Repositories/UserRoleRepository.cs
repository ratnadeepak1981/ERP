using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly SecurityDbContext _context;

    public UserRoleRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    public Task<UserRole?> GetAsync(
        Guid userId,
        Guid roleId)
    {
        return _context.UserRoles
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.RoleId == roleId);
    }

    public async Task AddAsync(
        UserRole userRole)
    {
        await _context.UserRoles.AddAsync(userRole);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}