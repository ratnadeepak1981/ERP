using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.MasterData;

public class LocationIdentifierRepository : ILocationIdentifierRepository
{
    private readonly DomainDbContext _context;

    public LocationIdentifierRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<WarehouseLocation?> GetLocationScopeAsync(Guid tenantId, Guid locationId)
    {
        return await _context.WarehouseLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == locationId);
    }

    public async Task<List<LocationIdentifier>> GetIdentifiersAsync(Guid tenantId, Guid locationId, bool includeInactive = false)
    {
        var query = _context.LocationIdentifiers
            .AsNoTracking()
            .Where(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId);

        if (!includeInactive)
        {
            query = query.Where(li => li.IsActive);
        }

        return await query.OrderByDescending(li => li.IsPrimary).ThenBy(li => li.CreatedAt).ToListAsync();
    }

    public async Task<LocationIdentifier?> GetIdentifierByIdAsync(Guid tenantId, Guid locationId, Guid identifierId)
    {
        return await _context.LocationIdentifiers
            .FirstOrDefaultAsync(li => li.TenantId == tenantId && li.WarehouseLocationId == locationId && li.LocationIdentifierId == identifierId);
    }

    public async Task<bool> ExistsActiveValueAsync(Guid tenantId, string normalizedValue)
    {
        return await _context.LocationIdentifiers
            .AnyAsync(li => li.TenantId == tenantId && li.IdentifierValue == normalizedValue && li.IsActive);
    }

    public async Task DemoteExistingPrimariesAsync(Guid tenantId, Guid locationId)
    {
        var existingPrimaries = await _context.LocationIdentifiers
            .Where(li => li.TenantId == tenantId &&
                         li.WarehouseLocationId == locationId &&
                         li.IsPrimary &&
                         li.IsActive)
            .ToListAsync();

        foreach (var primary in existingPrimaries)
        {
            primary.DemoteFromPrimary();
        }
    }

    public async Task AddIdentifierAsync(LocationIdentifier identifier)
    {
        await _context.LocationIdentifiers.AddAsync(identifier);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Detach(LocationIdentifier identifier)
    {
        _context.Entry(identifier).State = EntityState.Detached;
    }
}
