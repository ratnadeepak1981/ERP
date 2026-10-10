using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly DomainDbContext _context;

    public UnitOfMeasureRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId)
    {
        return await _context.UnitsOfMeasure
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<UnitOfMeasure?> GetUnitByIdAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UnitOfMeasureId == unitOfMeasureId && x.IsActive);
    }

    public async Task<UnitOfMeasure?> GetUnitByCodeAsync(Guid tenantId, string code)
    {
        string normalized = code.Trim().ToUpper();
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == normalized && x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code)
    {
        string normalized = code.Trim().ToUpper();
        return await _context.UnitsOfMeasure
            .AnyAsync(x => x.TenantId == tenantId && x.Code == normalized);
    }

    public async Task AddUnitAsync(UnitOfMeasure unitOfMeasure)
    {
        await _context.UnitsOfMeasure.AddAsync(unitOfMeasure);
    }

    public async Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId)
    {
        return await _context.UnitOfMeasureConversions
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<UnitOfMeasureConversion?> GetConversionByIdAsync(Guid tenantId, Guid conversionId)
    {
        return await _context.UnitOfMeasureConversions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ConversionId == conversionId && x.IsActive);
    }

    public async Task<UnitOfMeasureConversion?> GetConversionAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId)
    {
        return await _context.UnitOfMeasureConversions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FromUnitId == fromUnitId && x.ToUnitId == toUnitId && x.IsActive);
    }

    public async Task AddConversionAsync(UnitOfMeasureConversion conversion)
    {
        await _context.UnitOfMeasureConversions.AddAsync(conversion);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
