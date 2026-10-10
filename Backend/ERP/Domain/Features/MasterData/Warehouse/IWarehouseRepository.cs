using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Warehouse;

public interface IWarehouseRepository
{
    Task<List<Warehouse>> GetWarehousesAsync(Guid tenantId);
    Task<Warehouse?> GetWarehouseByIdAsync(Guid tenantId, Guid warehouseId);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsByNameAsync(Guid tenantId, string name);
    Task AddWarehouseAsync(Warehouse warehouse);

    Task<List<WarehouseZone>> GetZonesAsync(Guid tenantId, Guid warehouseId);
    Task<WarehouseZone?> GetZoneByIdAsync(Guid tenantId, Guid zoneId);
    Task<bool> ZoneExistsByCodeAsync(Guid tenantId, Guid warehouseId, string code);
    Task AddZoneAsync(WarehouseZone zone);

    Task<List<WarehouseLocation>> GetLocationsAsync(Guid tenantId, Guid warehouseId, Guid? zoneId = null);
    Task<WarehouseLocation?> GetLocationByIdAsync(Guid tenantId, Guid locationId);
    Task<bool> LocationExistsByCodeAsync(Guid tenantId, Guid warehouseId, string code);
    Task AddLocationAsync(WarehouseLocation location);

    Task<List<LocationType>> GetLocationTypesAsync(Guid tenantId);
    Task<LocationType?> GetLocationTypeByIdAsync(Guid? tenantId, Guid locationTypeId);
    Task<LocationType?> GetLocationTypeByCodeAsync(Guid? tenantId, string code);
    Task AddLocationTypeAsync(LocationType locationType);

    Task<bool> CompanyBelongsToTenantAsync(Guid tenantId, Guid companyId);
    Task<bool> BranchBelongsToCompanyAndTenantAsync(Guid tenantId, Guid companyId, Guid branchId);

    Task<List<WarehouseAddress>> GetAddressesAsync(Guid tenantId, Guid warehouseId);
    Task<WarehouseAddress?> GetAddressAssociationAsync(Guid tenantId, Guid warehouseId, Guid addressId, Guid addressTypeId);
    Task<WarehouseAddress?> GetAddressAssociationByIdAsync(Guid tenantId, Guid warehouseAddressId);
    Task AddAddressAssociationAsync(WarehouseAddress association);

    Task SaveChangesAsync();
}
