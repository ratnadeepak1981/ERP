using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Product;

public class ProductPackagingRepository : IProductPackagingRepository
{
    private readonly DomainDbContext _context;

    public ProductPackagingRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<ProductPackaging?> GetByIdAsync(Guid tenantId, Guid productPackagingId)
    {
        return await _context.ProductPackagings
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductPackagingId == productPackagingId);
    }

    public async Task<ProductPackaging?> GetByCodeAsync(Guid tenantId, string packagingCode)
    {
        string normalized = packagingCode.Trim().ToUpperInvariant();
        return await _context.ProductPackagings
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PackagingCode == normalized);
    }

    public async Task<List<ProductPackaging>> GetByProductIdAsync(Guid tenantId, Guid productId, bool includeInactive = false)
    {
        var query = _context.ProductPackagings
            .Where(p => p.TenantId == tenantId && p.ProductId == productId);

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderBy(p => p.PackagingCode).ToListAsync();
    }

    public async Task<List<ProductPackaging>> GetByProductAndUomAsync(Guid tenantId, Guid productId, Guid packagingUomId)
    {
        return await _context.ProductPackagings
            .Where(p => p.TenantId == tenantId
                     && p.ProductId == productId
                     && p.PackagingUomId == packagingUomId
                     && p.IsActive)
            .OrderBy(p => p.PackagingCode)
            .ToListAsync();
    }

    public async Task<List<ProductPackaging>> GetByTenantAsync(Guid tenantId, Guid? productId = null, Guid? companyId = null, bool includeInactive = false)
    {
        var query = _context.ProductPackagings
            .Where(p => p.TenantId == tenantId);

        if (productId.HasValue)
        {
            query = query.Where(p => p.ProductId == productId.Value);
        }

        if (companyId.HasValue)
        {
            query = query.Where(p => p.CompanyId == null || p.CompanyId == companyId.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderBy(p => p.PackagingCode).ToListAsync();
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string packagingCode, Guid? excludeId = null)
    {
        string normalized = packagingCode.Trim().ToUpperInvariant();
        var query = _context.ProductPackagings
            .Where(p => p.TenantId == tenantId && p.PackagingCode == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.ProductPackagingId != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task AddAsync(ProductPackaging packaging)
    {
        await _context.ProductPackagings.AddAsync(packaging);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
