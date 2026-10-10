using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Warehouse;

[ApiController]
[Route("api/warehouses")]
[RequirePermission("WAREHOUSE.VIEW")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;
    private readonly ICurrentUserContext _currentUserContext;

    public WarehouseController(
        IWarehouseService warehouseService,
        ICurrentUserContext currentUserContext)
    {
        _warehouseService = warehouseService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetWarehouses()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var warehouses = await _warehouseService.GetWarehousesAsync(tenantId.Value);
        return Ok(warehouses);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWarehouse(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var warehouse = await _warehouseService.GetWarehouseAsync(tenantId.Value, id);
        if (warehouse == null)
            return NotFound(new { status = "FAIL", message = "Warehouse not found." });

        return Ok(warehouse);
    }

    [HttpPost]
    [RequirePermission("WAREHOUSE.CREATE")]
    public async Task<IActionResult> CreateWarehouse([FromBody] CreateWarehouseRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var warehouse = await _warehouseService.CreateWarehouseAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetWarehouse), new { id = warehouse.WarehouseId }, warehouse);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("WAREHOUSE.UPDATE")]
    public async Task<IActionResult> UpdateWarehouse(Guid id, [FromBody] UpdateWarehouseRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var warehouse = await _warehouseService.UpdateWarehouseAsync(tenantId.Value, id, request);
            return Ok(warehouse);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("WAREHOUSE.DELETE")]
    public async Task<IActionResult> DeactivateWarehouse(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _warehouseService.DeactivateWarehouseAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse not found." });
        }
    }

    // Zones
    [HttpGet("{id:guid}/zones")]
    public async Task<IActionResult> GetZones(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var zones = await _warehouseService.GetZonesAsync(tenantId.Value, id);
        return Ok(zones);
    }

    [HttpPost("{id:guid}/zones")]
    [RequirePermission("WAREHOUSE.MANAGE_ZONES")]
    public async Task<IActionResult> CreateZone(Guid id, [FromBody] CreateWarehouseZoneRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var zone = await _warehouseService.CreateZoneAsync(tenantId.Value, id, request);
            return Ok(zone);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    // Locations
    [HttpGet("{id:guid}/locations")]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetLocations(Guid id, [FromQuery] Guid? zoneId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var locations = await _warehouseService.GetLocationsAsync(tenantId.Value, id, zoneId);
        return Ok(locations);
    }

    [HttpPost("{id:guid}/locations")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocationDirect(Guid id, [FromBody] CreateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var location = await _warehouseService.CreateLocationAsync(tenantId.Value, id, request.WarehouseZoneId, request);
            return Ok(location);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/zones/{zoneId:guid}/locations")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocation(Guid id, Guid zoneId, [FromBody] CreateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var location = await _warehouseService.CreateLocationAsync(tenantId.Value, id, zoneId, request);
            return Ok(location);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { status = "FAIL", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("locations/{locationId:guid}")]
    [RequirePermission("LOCATION.UPDATE")]
    public async Task<IActionResult> UpdateLocation(Guid locationId, [FromBody] UpdateWarehouseLocationRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var location = await _warehouseService.UpdateLocationAsync(tenantId.Value, locationId, request);
            return Ok(location);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse location not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("locations/{locationId:guid}")]
    [RequirePermission("LOCATION.DELETE")]
    public async Task<IActionResult> DeactivateLocation(Guid locationId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _warehouseService.DeactivateLocationAsync(tenantId.Value, locationId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse location not found." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
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
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var types = await _warehouseService.GetLocationTypesAsync(tenantId.Value);
        return Ok(types);
    }

    [HttpPost("location-types")]
    [RequirePermission("LOCATION.CREATE")]
    public async Task<IActionResult> CreateLocationType([FromBody] CreateLocationTypeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var locType = await _warehouseService.CreateLocationTypeAsync(tenantId.Value, request);
            return Ok(locType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }

    // Warehouse Addresses
    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var addresses = await _warehouseService.GetAddressesAsync(tenantId.Value, id);
        return Ok(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] WarehouseAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var assoc = await _warehouseService.LinkAddressAsync(tenantId.Value, id, request);
            return Ok(assoc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Warehouse not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { status = "FAIL", message = ex.Message });
        }
    }
}
