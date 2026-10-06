using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly SaaSDbContext _context;

    public TenantRepository(SaaSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
    }

    public async Task<Tenant?> GetByIdAsync(Guid tenantId)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId);
    }

    public async Task<bool> ExistsByCodeAsync(string code)
    {
        return await _context.Tenants
            .AnyAsync(x => x.Code == code);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync(
        IDbContextTransaction transaction)
    {
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
    }

    public async Task RollbackTransactionAsync(
        IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }
}