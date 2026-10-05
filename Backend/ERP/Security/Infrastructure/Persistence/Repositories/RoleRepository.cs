using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly SecurityDbContext _context;

    public RoleRepository(SecurityDbContext context)
    {
        _context = context;
    }

    public Task<Role?> GetByNameAsync(
        Guid? tenantId,
        string name)
    {
        return _context.Roles
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Name == name);
    }

    public async Task AddAsync(Role role)
    {
        await _context.Roles.AddAsync(role);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}