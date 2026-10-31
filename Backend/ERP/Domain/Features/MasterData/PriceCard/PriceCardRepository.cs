using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.PriceCard;

public class PriceCardRepository : IPriceCardRepository
{
    private readonly DomainDbContext _context;

    public PriceCardRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<PriceCard>> GetPriceCardsAsync(Guid tenantId, PriceCardType? type = null, string? search = null, bool? isActive = null)
    {
        var query = _context.PriceCards
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (type.HasValue)
        {
            query = query.Where(p => p.PriceCardType == type.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.PriceCardCode.Contains(term) || p.PriceCardName.Contains(term));
        }

        return await query
            .OrderBy(p => p.PriceCardCode)
            .ToListAsync();
    }

    public async Task<PriceCard?> GetPriceCardByIdAsync(Guid tenantId, Guid priceCardId, bool includeLines = true)
    {
        var query = _context.PriceCards.AsQueryable();

        if (includeLines)
        {
            query = query.Include(p => p.Lines);
        }

        return await query
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PriceCardId == priceCardId);
    }

    public async Task<PriceCard?> GetPriceCardByCodeAsync(Guid tenantId, string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.PriceCards
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PriceCardCode == normalized);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var query = _context.PriceCards
            .Where(p => p.TenantId == tenantId && p.PriceCardCode == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.PriceCardId != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task AddPriceCardAsync(PriceCard priceCard)
    {
        await _context.PriceCards.AddAsync(priceCard);
    }

    public async Task<List<PriceCardLine>> GetLinesByCardIdAsync(Guid tenantId, Guid priceCardId)
    {
        return await _context.PriceCardLines
            .AsNoTracking()
            .Include(l => l.Product)
            .Include(l => l.UnitOfMeasure)
            .Where(l => l.TenantId == tenantId && l.PriceCardId == priceCardId)
            .OrderBy(l => l.Product != null ? l.Product.ProductCode : string.Empty)
            .ToListAsync();
    }

    public async Task<PriceCardLine?> GetLineByIdAsync(Guid tenantId, Guid priceCardId, Guid lineId)
    {
        return await _context.PriceCardLines
            .Include(l => l.Product)
            .Include(l => l.UnitOfMeasure)
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.PriceCardId == priceCardId && l.PriceCardLineId == lineId);
    }

    public async Task AddLineAsync(PriceCardLine line)
    {
        await _context.PriceCardLines.AddAsync(line);
    }

    public Task RemoveLineAsync(PriceCardLine line)
    {
        _context.PriceCardLines.Remove(line);
        return Task.CompletedTask;
    }

    public async Task RemoveLinesAsync(Guid tenantId, Guid priceCardId)
    {
        var lines = await _context.PriceCardLines
            .Where(l => l.TenantId == tenantId && l.PriceCardId == priceCardId)
            .ToListAsync();

        if (lines.Count > 0)
        {
            _context.PriceCardLines.RemoveRange(lines);
        }
    }

    public async Task<PriceCardLine?> GetActiveLineAsync(
        Guid tenantId,
        Guid priceCardId,
        Guid productId,
        Guid? uomId,
        DateTime asOfDate)
    {
        var query = _context.PriceCardLines
            .AsNoTracking()
            .Include(l => l.Product)
            .Include(l => l.UnitOfMeasure)
            .Where(l => l.TenantId == tenantId &&
                        l.PriceCardId == priceCardId &&
                        l.ProductId == productId &&
                        l.IsActive &&
                        (!l.EffectiveFrom.HasValue || l.EffectiveFrom.Value <= asOfDate) &&
                        (!l.EffectiveTo.HasValue || l.EffectiveTo.Value >= asOfDate));

        // Exact UOM match if uomId provided
        if (uomId.HasValue)
        {
            var exact = await query
                .Where(l => l.UnitOfMeasureId == uomId.Value)
                .OrderByDescending(l => l.EffectiveFrom ?? DateTime.MinValue)
                .FirstOrDefaultAsync();

            if (exact != null) return exact;
        }

        // Otherwise first active line for product
        return await query
            .OrderByDescending(l => l.EffectiveFrom ?? DateTime.MinValue)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ProductExistsAsync(Guid tenantId, Guid productId)
    {
        return await _context.Products
            .AnyAsync(p => p.TenantId == tenantId && p.ProductId == productId);
    }

    public async Task<bool> UnitOfMeasureExistsAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        return await _context.UnitsOfMeasure
            .AnyAsync(u => u.TenantId == tenantId && u.UnitOfMeasureId == unitOfMeasureId);
    }

    private static readonly HashSet<string> StandardIsoCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "CAD", "AUD", "JPY", "CHF", "CNY", "INR", "LKR", "SGD", "NZD", "AED", "SAR"
    };

    public async Task<bool> CurrencyExistsAsync(Guid tenantId, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return false;
        var normalized = currencyCode.Trim().ToUpperInvariant();

        // 1. Check if configured in tenant's active currencies
        var existsInTenant = await _context.Currencies
            .AnyAsync(c => c.TenantId == tenantId && c.CurrencyCode == normalized && c.IsActive);
        if (existsInTenant) return true;

        // 2. Check if it matches standard ISO currency codes
        return StandardIsoCurrencies.Contains(normalized);
    }

    public async Task<bool> LineExistsAsync(Guid tenantId, Guid priceCardId, Guid productId, Guid? uomId, Guid? excludeLineId = null)
    {
        var query = _context.PriceCardLines
            .Where(l => l.TenantId == tenantId &&
                        l.PriceCardId == priceCardId &&
                        l.ProductId == productId &&
                        l.UnitOfMeasureId == uomId);

        if (excludeLineId.HasValue)
        {
            query = query.Where(l => l.PriceCardLineId != excludeLineId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
