using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Supplier;

public class SupplierProductPriceRepository : ISupplierProductPriceRepository
{
    private readonly DomainDbContext _context;

    public SupplierProductPriceRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierProductPrice>> GetPricesAsync(Guid tenantId, Guid? supplierId = null, Guid? productId = null)
    {
        var query = _context.SupplierProductPrices
            .Where(x => x.TenantId == tenantId && x.IsActive);

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == supplierId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<SupplierProductPrice?> GetPriceByIdAsync(Guid tenantId, Guid priceId)
    {
        return await _context.SupplierProductPrices
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SupplierProductPriceId == priceId && x.IsActive);
    }

    public async Task<SupplierProductPrice?> GetActivePriceAsync(Guid tenantId, Guid supplierId, Guid productId, DateTime asOfDate)
    {
        return await _context.SupplierProductPrices
            .Where(x => x.TenantId == tenantId &&
                        x.SupplierId == supplierId &&
                        x.ProductId == productId &&
                        x.IsActive &&
                        x.EffectiveFrom <= asOfDate &&
                        (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SupplierProductPrice>> GetPreferredPricesAsync(Guid tenantId, Guid productId)
    {
        return await _context.SupplierProductPrices
            .Where(x => x.TenantId == tenantId && x.ProductId == productId && x.IsPreferred && x.IsActive)
            .ToListAsync();
    }

    public async Task AddPriceAsync(SupplierProductPrice price)
    {
        await _context.SupplierProductPrices.AddAsync(price);
    }

    public async Task<bool> SupplierExistsAsync(Guid tenantId, Guid supplierId)
    {
        return await _context.Suppliers
            .AnyAsync(x => x.TenantId == tenantId && x.SupplierId == supplierId && x.IsActive);
    }

    public async Task<bool> ProductExistsAsync(Guid tenantId, Guid productId)
    {
        return await _context.Products
            .AnyAsync(x => x.TenantId == tenantId && x.ProductId == productId && x.IsActive);
    }

    public async Task<bool> UnitOfMeasureExistsAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        return await _context.UnitsOfMeasure
            .AnyAsync(x => x.TenantId == tenantId && x.UnitOfMeasureId == unitOfMeasureId && x.IsActive);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
