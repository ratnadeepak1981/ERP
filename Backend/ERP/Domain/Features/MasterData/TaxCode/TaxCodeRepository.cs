using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.TaxCode;

public class TaxCodeRepository : ITaxCodeRepository
{
    private readonly DomainDbContext _context;

    public TaxCodeRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<TaxCode>> GetTaxCodesAsync(
        Guid tenantId,
        bool includeInactive = false,
        TaxApplicability? applicability = null)
    {
        var query = _context.TaxCodes
            .Where(t => t.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        if (applicability.HasValue)
        {
            query = query.Where(t => t.Applicability == TaxApplicability.Both || t.Applicability == applicability.Value);
        }

        return await query
            .OrderBy(t => t.Code)
            .ToListAsync();
    }

    public async Task<TaxCode?> GetTaxCodeByIdAsync(Guid tenantId, Guid taxCodeId)
    {
        return await _context.TaxCodes
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.TaxCodeId == taxCodeId);
    }

    public async Task<TaxCode?> GetTaxCodeByCodeAsync(Guid tenantId, string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.TaxCodes
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == normalized);
    }

    public async Task<bool> ExistsCodeAsync(Guid tenantId, string code, Guid? excludeId = null)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.TaxCodes
            .AnyAsync(t => t.TenantId == tenantId && t.Code == normalized && (!excludeId.HasValue || t.TaxCodeId != excludeId.Value));
    }

    public async Task AddAsync(TaxCode taxCode)
    {
        await _context.TaxCodes.AddAsync(taxCode);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
