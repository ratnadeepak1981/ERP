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