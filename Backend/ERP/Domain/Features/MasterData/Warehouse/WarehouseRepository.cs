using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly DomainDbContext _context;

    public WarehouseRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Warehouse>> GetWarehousesAsync(Guid tenantId)
    {
        return await _context.Warehouses
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Warehouse?> GetWarehouseByIdAsync(Guid tenantId, Guid warehouseId)
    {
        return await _context.Warehouses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code)
    {
        return await _context.Warehouses
            .AnyAsync(x => x.TenantId == tenantId && x.WarehouseCode == code);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string name)
    {
        return await _context.Warehouses
            .AnyAsync(x => x.TenantId == tenantId && x.WarehouseName == name);
    }

    public async Task AddWarehouseAsync(Warehouse warehouse)
    {
        await _context.Warehouses.AddAsync(warehouse);
    }

    public async Task<List<WarehouseZone>> GetZonesAsync(Guid tenantId, Guid warehouseId)
    {
        return await _context.WarehouseZones
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.IsActive)
            .ToListAsync();
    }

    public async Task<WarehouseZone?> GetZoneByIdAsync(Guid tenantId, Guid zoneId)
    {
        return await _context.WarehouseZones
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseZoneId == zoneId && x.IsActive);
    }

    public async Task<bool> ZoneExistsByCodeAsync(Guid tenantId, Guid warehouseId, string code)
    {
        return await _context.WarehouseZones
            .AnyAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.ZoneCode == code);
    }

    public async Task AddZoneAsync(WarehouseZone zone)
    {
        await _context.WarehouseZones.AddAsync(zone);
    }

    public async Task<List<WarehouseLocation>> GetLocationsAsync(Guid tenantId, Guid warehouseId, Guid? zoneId = null)
    {
        var query = _context.WarehouseLocations
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.IsActive);

        if (zoneId.HasValue)
        {
            query = query.Where(x => x.WarehouseZoneId == zoneId.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<WarehouseLocation?> GetLocationByIdAsync(Guid tenantId, Guid locationId)
    {
        return await _context.WarehouseLocations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseLocationId == locationId && x.IsActive);
    }

    public async Task<bool> LocationExistsByCodeAsync(Guid tenantId, Guid warehouseId, string code)
    {
        return await _context.WarehouseLocations
            .AnyAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.LocationCode == code);
    }

    public async Task AddLocationAsync(WarehouseLocation location)
    {
        await _context.WarehouseLocations.AddAsync(location);
    }

    public async Task<List<LocationType>> GetLocationTypesAsync(Guid tenantId)
    {
        return await _context.LocationTypes
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync();
    }

    public async Task<LocationType?> GetLocationTypeByIdAsync(Guid? tenantId, Guid locationTypeId)
    {
        return await _context.LocationTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.LocationTypeId == locationTypeId && x.IsActive);
    }

    public async Task<LocationType?> GetLocationTypeByCodeAsync(Guid? tenantId, string code)
    {
        string normalized = code.Trim().ToUpperInvariant();
        return await _context.LocationTypes
            .FirstOrDefaultAsync(x => (x.TenantId == null || x.TenantId == tenantId) && x.Code == normalized && x.IsActive);
    }

    public async Task AddLocationTypeAsync(LocationType locationType)
    {
        await _context.LocationTypes.AddAsync(locationType);
    }

    public async Task<bool> CompanyBelongsToTenantAsync(Guid tenantId, Guid companyId)
    {
        return await _context.Companies
            .AnyAsync(x => x.TenantId == tenantId && x.Id == companyId && x.IsActive);
    }

    public async Task<bool> BranchBelongsToCompanyAndTenantAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        return await _context.Branches
            .AnyAsync(x => x.CompanyId == companyId && x.Id == branchId && x.IsActive);
    }

    public async Task<List<WarehouseAddress>> GetAddressesAsync(Guid tenantId, Guid warehouseId)
    {
        return await _context.WarehouseAddresses
            .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.IsActive)
            .ToListAsync();
    }

    public async Task<WarehouseAddress?> GetAddressAssociationAsync(Guid tenantId, Guid warehouseId, Guid addressId, Guid addressTypeId)
    {
        return await _context.WarehouseAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.AddressId == addressId && x.AddressTypeId == addressTypeId && x.IsActive);
    }

    public async Task<WarehouseAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid warehouseAddressId)
    {
        return await _context.WarehouseAddresses
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseAddressId == warehouseAddressId && x.IsActive);
    }

    public async Task AddAddressAssociationAsync(WarehouseAddress association)
    {
        await _context.WarehouseAddresses.AddAsync(association);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
