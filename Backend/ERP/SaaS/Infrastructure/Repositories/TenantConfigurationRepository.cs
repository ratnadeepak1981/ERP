using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces.Repositories;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class TenantConfigurationRepository
    : ITenantConfigurationRepository
{
    private readonly SaaSDbContext _context;

    public TenantConfigurationRepository(
        SaaSDbContext context)
    {
        _context = context;
    }

    public async Task<TenantConfiguration?> GetByTenantIdAsync(
        Guid tenantId)
    {
        return await _context.TenantConfigurations
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId);
    }

    public async Task<bool> ExistsByTenantIdAsync(
        Guid tenantId)
    {
        return await _context.TenantConfigurations
            .AnyAsync(
                x => x.TenantId == tenantId);
    }

    public async Task AddAsync(
        TenantConfiguration configuration)
    {
        await _context.TenantConfigurations.AddAsync(
            configuration);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}