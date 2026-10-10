using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.UnitOfMeasure;

[ApiController]
[Route("api/uom")]
[RequirePermission("UOM.VIEW")]
public class UnitOfMeasureController : ControllerBase
{
    private readonly IUnitOfMeasureService _uomService;
    private readonly ICurrentUserContext _currentUserContext;

    public UnitOfMeasureController(
        IUnitOfMeasureService uomService,
        ICurrentUserContext currentUserContext)
    {
        _uomService = uomService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnits()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var units = await _uomService.GetUnitsAsync(tenantId.Value);
        return Ok(units);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUnit(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var unit = await _uomService.GetUnitAsync(tenantId.Value, id);
        if (unit == null)
            return NotFound(new { status = "FAIL", message = "Unit of measure not found." });

        return Ok(unit);
    }

    [HttpPost]
    [RequirePermission("UOM.CREATE")]
    public async Task<IActionResult> CreateUnit([FromBody] CreateUnitOfMeasureRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var unit = await _uomService.CreateUnitAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetUnit), new { id = unit.UnitOfMeasureId }, unit);
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
    [RequirePermission("UOM.UPDATE")]
    public async Task<IActionResult> UpdateUnit(Guid id, [FromBody] UpdateUnitOfMeasureRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var unit = await _uomService.UpdateUnitAsync(tenantId.Value, id, request);
            return Ok(unit);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Unit of measure not found." });
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
    [RequirePermission("UOM.DELETE")]
    public async Task<IActionResult> DeactivateUnit(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _uomService.DeactivateUnitAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Unit of measure not found." });
        }
    }

    [HttpGet("conversions")]
    public async Task<IActionResult> GetConversions()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var conversions = await _uomService.GetConversionsAsync(tenantId.Value);
        return Ok(conversions);
    }

    [HttpPost("conversions")]
    [RequirePermission("UOM.CONVERT")]
    public async Task<IActionResult> CreateConversion([FromBody] CreateUnitOfMeasureConversionRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var conversion = await _uomService.CreateConversionAsync(tenantId.Value, request);
            return Ok(conversion);
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
