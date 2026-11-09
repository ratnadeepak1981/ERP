using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using UnitOfMeasureEntity = Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure;

namespace ERP.Infrastructure.Repositories.MasterData;

public class ProductIdentifierRepository : IProductIdentifierRepository
{
    private readonly DomainDbContext _context;

    public ProductIdentifierRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Product?> GetProductAsync(Guid tenantId, Guid productId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == productId);
    }

    public async Task<List<ProductIdentifier>> GetIdentifiersAsync(Guid tenantId, Guid productId, bool includeInactive = false)
    {
        var query = _context.ProductIdentifiers
            .AsNoTracking()
            .Where(pi => pi.TenantId == tenantId && pi.ProductId == productId);

        if (!includeInactive)
        {
            query = query.Where(pi => pi.IsActive);
        }

        return await query.OrderByDescending(pi => pi.IsPrimary).ThenBy(pi => pi.CreatedAt).ToListAsync();
    }

    public async Task<ProductIdentifier?> GetIdentifierByIdAsync(Guid tenantId, Guid productId, Guid identifierId)
    {
        return await _context.ProductIdentifiers
            .FirstOrDefaultAsync(pi => pi.TenantId == tenantId && pi.ProductId == productId && pi.ProductIdentifierId == identifierId);
    }

    public async Task<Dictionary<Guid, string>> GetUomCodesAsync(Guid tenantId, IEnumerable<Guid> uomIds)
    {
        var ids = uomIds.Distinct().ToList();
        if (!ids.Any())
        {
            return new Dictionary<Guid, string>();
        }

        return await _context.UnitsOfMeasure
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && ids.Contains(u.UnitOfMeasureId))
            .ToDictionaryAsync(u => u.UnitOfMeasureId, u => u.Code);
    }

    public async Task<string?> GetUomCodeAsync(Guid tenantId, Guid uomId)
    {
        var uom = await _context.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == uomId);

        return uom?.Code;
    }

    public async Task<UnitOfMeasureEntity?> GetUomByIdAsync(Guid tenantId, Guid uomId)
    {
        return await _context.UnitsOfMeasure
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == uomId);
    }

    public async Task<bool> ExistsActiveValueAsync(Guid tenantId, string normalizedValue)
    {
        return await _context.ProductIdentifiers
            .AnyAsync(pi => pi.TenantId == tenantId && pi.IdentifierValue == normalizedValue && pi.IsActive);
    }

    public async Task DemoteExistingPrimariesAsync(Guid tenantId, Guid productId, Guid? unitOfMeasureId)
    {
        var existingPrimaries = await _context.ProductIdentifiers
            .Where(pi => pi.TenantId == tenantId &&
                         pi.ProductId == productId &&
                         pi.UnitOfMeasureId == unitOfMeasureId &&
                         pi.IsPrimary &&
                         pi.IsActive)
            .ToListAsync();

        foreach (var primary in existingPrimaries)
        {
            primary.DemoteFromPrimary();
        }
    }

    public async Task AddIdentifierAsync(ProductIdentifier identifier)
    {
        await _context.ProductIdentifiers.AddAsync(identifier);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Detach(ProductIdentifier identifier)
    {
        _context.Entry(identifier).State = EntityState.Detached;
    }
}
