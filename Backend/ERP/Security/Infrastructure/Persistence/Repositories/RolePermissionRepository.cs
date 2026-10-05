using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Interfaces;

namespace Security.Infrastructure.Persistence.Repositories;

public class RolePermissionRepository
    : IRolePermissionRepository
{
    private readonly SecurityDbContext _context;

    public RolePermissionRepository(
        SecurityDbContext context)
    {
        _context = context;
    }

    public Task<RolePermission?> GetAsync(
        Guid roleId,
        Guid permissionId)
    {
        return _context.RolePermissions
            .FirstOrDefaultAsync(x =>
                x.RoleId == roleId &&
                x.PermissionId == permissionId);
    }

    public async Task AddAsync(
        RolePermission rolePermission)
    {
        await _context.RolePermissions.AddAsync(
            rolePermission);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}