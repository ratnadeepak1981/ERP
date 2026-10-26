using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Currency;

public class CurrencyRepository : ICurrencyRepository
{
    private readonly DomainDbContext _context;

    public CurrencyRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Currency>> GetCurrenciesAsync(Guid tenantId)
    {
        return await _context.Currencies
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CurrencyCode)
            .ToListAsync();
    }

    public async Task<Currency?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId)
    {
        return await _context.Currencies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyId == currencyId);
    }

    public async Task<Currency?> GetCurrencyByCodeAsync(Guid tenantId, string currencyCode)
    {
        var normalizedCode = currencyCode.Trim().ToUpperInvariant();
        return await _context.Currencies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == normalizedCode);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string currencyCode, Guid? excludeId = null)
    {
        var normalizedCode = currencyCode.Trim().ToUpperInvariant();
        return await _context.Currencies
            .AnyAsync(c => c.TenantId == tenantId &&
                           c.CurrencyCode == normalizedCode &&
                           (!excludeId.HasValue || c.CurrencyId != excludeId.Value));
    }

    public async Task AddCurrencyAsync(Currency currency)
    {
        await _context.Currencies.AddAsync(currency);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
