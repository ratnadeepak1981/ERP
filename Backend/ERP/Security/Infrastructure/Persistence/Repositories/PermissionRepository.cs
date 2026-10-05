using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly SecurityDbContext _context;

    public PermissionRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    public Task<Permission?> GetByCodeAsync(
        string code)
    {
        return _context.Permissions
            .FirstOrDefaultAsync(x =>
                x.Code == code);
    }

    public async Task AddAsync(
        Permission permission)
    {
        await _context.Permissions.AddAsync(
            permission);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}