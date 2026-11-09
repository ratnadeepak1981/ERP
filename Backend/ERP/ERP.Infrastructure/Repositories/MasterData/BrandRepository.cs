using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Brand;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.MasterData;

public class BrandRepository : IBrandRepository
{
    private readonly DomainDbContext _context;

    public BrandRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Brand>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<Brand>()
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.BrandCode)
            .ToListAsync();
    }

    public async Task<Brand?> GetByIdAsync(Guid tenantId, Guid brandId)
    {
        return await _context.Set<Brand>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.BrandId == brandId);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string brandCode)
    {
        return await _context.Set<Brand>()
            .AnyAsync(b => b.TenantId == tenantId && b.BrandCode == brandCode);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string brandName)
    {
        return await _context.Set<Brand>()
            .AnyAsync(b => b.TenantId == tenantId && b.BrandName == brandName);
    }

    public async Task AddAsync(Brand brand)
    {
        if (brand == null) throw new ArgumentNullException(nameof(brand));
        await _context.Set<Brand>().AddAsync(brand);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
