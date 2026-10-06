using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class TenantDatabaseRepository
    : ITenantDatabaseRepository
{
    private readonly SaaSDbContext _context;

    public TenantDatabaseRepository(
        SaaSDbContext context)
    {
        _context = context;
    }

    public async Task<TenantDatabase?> GetByTenantIdAsync(
        Guid tenantId)
    {
        return await _context.TenantDatabases
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByTenantIdAsync(
        Guid tenantId)
    {
        return await _context.TenantDatabases
            .AnyAsync(
                x => x.TenantId == tenantId);
    }

    public async Task AddAsync(
        TenantDatabase tenantDatabase)
    {
        await _context.TenantDatabases.AddAsync(
            tenantDatabase);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}