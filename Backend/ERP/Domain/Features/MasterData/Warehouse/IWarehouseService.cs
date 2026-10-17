using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.MasterData.Warehouse;

public interface IWarehouseService
{
    Task<List<Warehouse>> GetWarehousesAsync(Guid tenantId);
    Task<Warehouse?> GetWarehouseAsync(Guid tenantId, Guid warehouseId);
    Task<Warehouse> CreateWarehouseAsync(Guid tenantId, CreateWarehouseRequest request);
    Task<Warehouse> UpdateWarehouseAsync(Guid tenantId, Guid warehouseId, UpdateWarehouseRequest request);
    Task DeactivateWarehouseAsync(Guid tenantId, Guid warehouseId);

    Task<List<WarehouseZone>> GetZonesAsync(Guid tenantId, Guid warehouseId);
    Task<WarehouseZone?> GetZoneAsync(Guid tenantId, Guid zoneId);
    Task<WarehouseZone> CreateZoneAsync(Guid tenantId, Guid warehouseId, CreateWarehouseZoneRequest request);
    Task<WarehouseZone> UpdateZoneAsync(Guid tenantId, Guid zoneId, UpdateWarehouseZoneRequest request);
    Task DeactivateZoneAsync(Guid tenantId, Guid zoneId);

    Task<List<WarehouseLocation>> GetLocationsAsync(Guid tenantId, Guid warehouseId, Guid? zoneId = null);
    Task<WarehouseLocation?> GetLocationAsync(Guid tenantId, Guid locationId);
    Task<WarehouseLocation> CreateLocationAsync(Guid tenantId, Guid warehouseId, Guid? zoneId, CreateWarehouseLocationRequest request);
    Task<WarehouseLocation> UpdateLocationAsync(Guid tenantId, Guid locationId, UpdateWarehouseLocationRequest request);
    Task DeactivateLocationAsync(Guid tenantId, Guid locationId);

    Task<List<LocationType>> GetLocationTypesAsync(Guid tenantId);
    Task<LocationType> CreateLocationTypeAsync(Guid tenantId, CreateLocationTypeRequest request);

    Task<List<WarehouseAddress>> GetAddressesAsync(Guid tenantId, Guid warehouseId);
    Task<WarehouseAddress> LinkAddressAsync(Guid tenantId, Guid warehouseId, WarehouseAddressLinkRequest request);
    Task SetDefaultAddressAsync(Guid tenantId, Guid warehouseId, Guid warehouseAddressId);
    Task RemoveAddressLinkAsync(Guid tenantId, Guid warehouseId, Guid warehouseAddressId);
}
