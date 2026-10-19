using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.UnitOfMeasure;

[ApiController]
[Route("api/uom")]
[RequirePermission("UOM.VIEW")]
public class UnitOfMeasureController : BaseApiController
{
    private readonly IUnitOfMeasureService _uomService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure> _sortMapping = new SortMapping<global::Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure>()
        .Add("code", u => u.Code, isDefault: true)
        .Add("name", u => u.Name)
        .SetSecondary(u => u.UnitOfMeasureId);

    public UnitOfMeasureController(
        IUnitOfMeasureService uomService,
        ICurrentUserContext currentUserContext)
    {
        _uomService = uomService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnits(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var units = await _uomService.GetUnitsAsync(tenantId.Value);
        return PaginateAndSort(units, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUnit(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var unit = await _uomService.GetUnitAsync(tenantId.Value, id);
        if (unit == null)
            return NotFoundResponse("Unit of measure not found.");

        return SuccessResponse(unit);
    }

    [HttpPost]
    [RequirePermission("UOM.CREATE")]
    public async Task<IActionResult> CreateUnit([FromBody] CreateUnitOfMeasureRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var unit = await _uomService.CreateUnitAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetUnit), new { id = unit.UnitOfMeasureId }, unit);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("UOM.UPDATE")]
    public async Task<IActionResult> UpdateUnit(Guid id, [FromBody] UpdateUnitOfMeasureRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var unit = await _uomService.UpdateUnitAsync(tenantId.Value, id, request);
            return SuccessResponse(unit);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("UOM.DELETE")]
    public async Task<IActionResult> DeactivateUnit(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _uomService.DeactivateUnitAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("conversions")]
    public async Task<IActionResult> GetConversions()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var conversions = await _uomService.GetConversionsAsync(tenantId.Value);
        return SuccessResponse(conversions);
    }

    [HttpPost("conversions")]
    [RequirePermission("UOM.CONVERT")]
    public async Task<IActionResult> CreateConversion([FromBody] CreateUnitOfMeasureConversionRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var conversion = await _uomService.CreateConversionAsync(tenantId.Value, request);
            return SuccessResponse(conversion, "Unit of measure conversion created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
