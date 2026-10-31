using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Warehouse;

[ApiController]
[Route("api/warehouses")]
[RequirePermission("WAREHOUSE.VIEW")]
public class WarehouseController : BaseApiController
{
    private readonly IWarehouseService _warehouseService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::ERP.Domain.Features.MasterData.Warehouse.Warehouse> _sortMapping = new SortMapping<global::ERP.Domain.Features.MasterData.Warehouse.Warehouse>()
        .Add("warehouseCode", w => w.WarehouseCode, isDefault: true)
        .Add("warehouseName", w => w.WarehouseName)
        .Add("city", w => w.City ?? "")
        .SetSecondary(w => w.WarehouseId);

    public WarehouseController(
        IWarehouseService warehouseService,
        ICurrentUserContext currentUserContext)
    {
        _warehouseService = warehouseService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetWarehouses(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var warehouses = await _warehouseService.GetWarehousesAsync(tenantId.Value);
        return PaginateAndSort(warehouses, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWarehouse(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var warehouse = await _warehouseService.GetWarehouseAsync(tenantId.Value, id);
        if (warehouse == null)
            return NotFoundResponse("Warehouse not found.");

        return SuccessResponse(warehouse);
    }

    [HttpPost]
    [RequirePermission("WAREHOUSE.CREATE")]
    public async Task<IActionResult> CreateWarehouse([FromBody] CreateWarehouseRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var warehouse = await _warehouseService.CreateWarehouseAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetWarehouse), new { id = warehouse.WarehouseId }, warehouse);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("WAREHOUSE.EDIT,WAREHOUSE.UPDATE")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] UpdateWarehouseRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var warehouse = await _warehouseService.UpdateWarehouseAsync(tenantId.Value, id, request);
            return SuccessResponse(warehouse);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission("WAREHOUSE.DEACTIVATE,WAREHOUSE.DELETE")]
    public async Task<IActionResult> DeactivateWarehouse(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _warehouseService.DeactivateWarehouseAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission("WAREHOUSE.ACTIVATE,WAREHOUSE.EDIT,WAREHOUSE.UPDATE")]
    public async Task<IActionResult> ActivateWarehouse(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var warehouse = await _warehouseService.ActivateWarehouseAsync(tenantId.Value, id);
            return SuccessResponse(warehouse, "Warehouse activated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    // Zones
    [HttpGet("{id:guid}/zones")]
    public async Task<IActionResult> GetZones(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var zones = await _warehouseService.GetZonesAsync(tenantId.Value, id);
        return SuccessResponse(zones);
    }

    [HttpGet("{id:guid}/zones/{zoneId:guid}")]
    [HttpGet("zones/{zoneId:guid}")]
    public async Task<IActionResult> GetZone(Guid? id, Guid zoneId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var zone = await _warehouseService.GetZoneAsync(tenantId.Value, zoneId);
        if (zone == null)
            return NotFoundResponse("Warehouse zone not found.");

        if (id.HasValue && zone.WarehouseId != id.Value)
            return ConflictResponse("The specified zone does not belong to the given warehouse.");

        return SuccessResponse(zone);
    }

    [HttpPost("{id:guid}/zones")]
    [RequirePermission("WAREHOUSE.CREATE,WAREHOUSE.MANAGE_ZONES")]
    public async Task<IActionResult> CreateZone(Guid id, [FromBody] CreateWarehouseZoneRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var zone = await _warehouseService.CreateZoneAsync(tenantId.Value, id, request);
            return SuccessResponse(zone, "Warehouse zone created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/zones/{zoneId:guid}")]
    [HttpPut("zones/{zoneId:guid}")]
    [RequirePermission("WAREHOUSE.EDIT,WAREHOUSE.MANAGE_ZONES")]
    public async Task<IActionResult> UpdateZone(Guid? id, Guid zoneId, [FromBody] UpdateWarehouseZoneRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var zone = await _warehouseService.UpdateZoneAsync(tenantId.Value, zoneId, request, id);
            return SuccessResponse(zone, "Warehouse zone updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/zones/{zoneId:guid}")]
    [HttpDelete("zones/{zoneId:guid}")]
    [HttpPost("{id:guid}/zones/{zoneId:guid}/deactivate")]
    [HttpPost("zones/{zoneId:guid}/deactivate")]
    [RequirePermission("WAREHOUSE.DEACTIVATE,WAREHOUSE.DELETE,WAREHOUSE.MANAGE_ZONES")]
    public async Task<IActionResult> DeactivateZone(Guid? id, Guid zoneId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _warehouseService.DeactivateZoneAsync(tenantId.Value, zoneId, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/zones/{zoneId:guid}/activate")]
    [HttpPost("zones/{zoneId:guid}/activate")]
    [RequirePermission("WAREHOUSE.ACTIVATE,WAREHOUSE.EDIT,WAREHOUSE.MANAGE_ZONES")]
    public async Task<IActionResult> ActivateZone(Guid? id, Guid zoneId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var zone = await _warehouseService.ActivateZoneAsync(tenantId.Value, zoneId, id);
            return SuccessResponse(zone, "Warehouse zone activated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    // Locations
    [HttpGet("{id:guid}/locations")]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetLocations(Guid id, [FromQuery] Guid? zoneId, [FromQuery] bool includeDetails = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        if (includeDetails)
        {
            var detailedLocations = await _warehouseService.GetLocationsWithDetailsAsync(tenantId.Value, id, zoneId);
            return SuccessResponse(detailedLocations);
        }

        var locations = await _warehouseService.GetLocationsAsync(tenantId.Value, id, zoneId);
        return SuccessResponse(locations);
    }

    [HttpPost("{id:guid}/locations")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocationDirect(Guid id, [FromBody] CreateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var location = await _warehouseService.CreateLocationAsync(tenantId.Value, id, request.WarehouseZoneId, request);
            return SuccessResponse(location, "Warehouse location created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/zones/{zoneId:guid}/locations")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocation(Guid id, Guid zoneId, [FromBody] CreateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var location = await _warehouseService.CreateLocationAsync(tenantId.Value, id, zoneId, request);
            return SuccessResponse(location, "Warehouse location created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("locations/{locationId:guid}")]
    [HttpGet("{id:guid}/locations/{locationId:guid}")]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetLocation(Guid? id, Guid locationId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var location = await _warehouseService.GetLocationAsync(tenantId.Value, locationId);
        if (location == null)
            return NotFoundResponse("Warehouse location not found.");

        if (id.HasValue && location.WarehouseId != id.Value)
            return ConflictResponse("The specified location does not belong to the given warehouse.");

        return SuccessResponse(location);
    }

    [HttpPut("locations/{locationId:guid}")]
    [RequirePermission("LOCATION.EDIT,LOCATION.UPDATE")]
    public async Task<IActionResult> UpdateLocation(Guid locationId, [FromBody] UpdateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var location = await _warehouseService.UpdateLocationAsync(tenantId.Value, locationId, request);
            return SuccessResponse(location, "Warehouse location updated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("locations/{locationId:guid}")]
    [HttpDelete("{id:guid}/locations/{locationId:guid}")]
    [HttpPost("locations/{locationId:guid}/deactivate")]
    [HttpPost("{id:guid}/locations/{locationId:guid}/deactivate")]
    [RequirePermission("LOCATION.DEACTIVATE,LOCATION.DELETE")]
    public async Task<IActionResult> DeactivateLocation(Guid locationId, Guid? id = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            if (id.HasValue)
            {
                var loc = await _warehouseService.GetLocationAsync(tenantId.Value, locationId);
                if (loc != null && loc.WarehouseId != id.Value)
                    return ConflictResponse("The specified location does not belong to the given warehouse.");
            }

            await _warehouseService.DeactivateLocationAsync(tenantId.Value, locationId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("locations/{locationId:guid}/activate")]
    [HttpPost("{id:guid}/locations/{locationId:guid}/activate")]
    [RequirePermission("LOCATION.ACTIVATE,LOCATION.EDIT,LOCATION.UPDATE")]
    public async Task<IActionResult> ActivateLocation(Guid locationId, Guid? id = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var location = await _warehouseService.ActivateLocationAsync(tenantId.Value, locationId);
            if (id.HasValue && location.WarehouseId != id.Value)
                return ConflictResponse("The specified location does not belong to the given warehouse.");

            return SuccessResponse(location, "Warehouse location activated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    // Location Types
    [HttpGet("location-types")]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetLocationTypes()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var types = await _warehouseService.GetLocationTypesAsync(tenantId.Value);
        return SuccessResponse(types);
    }

    [HttpPost("location-types")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocationType([FromBody] CreateLocationTypeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var locType = await _warehouseService.CreateLocationTypeAsync(tenantId.Value, request);
            return SuccessResponse(locType, "Location type created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    // Warehouse Addresses
    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var addresses = await _warehouseService.GetAddressesAsync(tenantId.Value, id);
        return SuccessResponse(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] WarehouseAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var assoc = await _warehouseService.LinkAddressAsync(tenantId.Value, id, request);
            return SuccessResponse(assoc, "Warehouse address linked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
