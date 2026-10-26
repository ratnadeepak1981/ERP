using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.TaxCode;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.TaxCode;

[ApiController]
[Route("api/tax-codes")]
[RequirePermission("TAX_CODE.VIEW")]
public class TaxCodesController : BaseApiController
{
    private readonly ITaxCodeService _taxCodeService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.TaxCode.TaxCode> _sortMapping =
        new SortMapping<global::Domain.Features.MasterData.TaxCode.TaxCode>()
            .Add("code", t => t.Code, isDefault: true)
            .Add("name", t => t.Name)
            .Add("rate", t => t.Rate)
            .Add("applicability", t => t.Applicability)
            .SetSecondary(t => t.TaxCodeId);

    public TaxCodesController(
        ITaxCodeService taxCodeService,
        ICurrentUserContext currentUserContext)
    {
        _taxCodeService = taxCodeService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetTaxCodes(
        [FromQuery] TaxApplicability? applicability = null,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var taxCodes = await _taxCodeService.GetTaxCodesAsync(tenantId.Value, includeInactive, applicability);

        if (page.HasValue || pageSize.HasValue)
        {
            int pageNumber = page.GetValueOrDefault(1);
            if (pageNumber < 1) pageNumber = 1;
            int limit = pageSize.GetValueOrDefault(20);
            if (limit < 1) limit = 1;
            int total = taxCodes.Count;
            var paged = taxCodes.Skip((pageNumber - 1) * limit).Take(limit).ToList();
            return Ok(new
            {
                success = true,
                data = paged,
                totalCount = total,
                page = pageNumber,
                pageSize = limit
            });
        }

        return SuccessResponse(taxCodes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTaxCode(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var taxCode = await _taxCodeService.GetTaxCodeAsync(tenantId.Value, id);
        if (taxCode == null)
            return NotFoundResponse("Tax code not found.");

        return SuccessResponse(taxCode);
    }

    [HttpPost]
    [RequirePermission("TAX_CODE.CREATE")]
    public async Task<IActionResult> CreateTaxCode([FromBody] CreateTaxCodeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var taxCode = await _taxCodeService.CreateTaxCodeAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetTaxCode), new { id = taxCode.TaxCodeId }, taxCode);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("TAX_CODE.EDIT")]
    public async Task<IActionResult> UpdateTaxCode(Guid id, [FromBody] UpdateTaxCodeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var taxCode = await _taxCodeService.UpdateTaxCodeAsync(tenantId.Value, id, request);
            return SuccessResponse(taxCode);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("TAX_CODE.DELETE")]
    public async Task<IActionResult> DeactivateTaxCode(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _taxCodeService.DeactivateTaxCodeAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("TAX_CODE.EDIT")]
    public async Task<IActionResult> ReactivateTaxCode(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var taxCode = await _taxCodeService.ReactivateTaxCodeAsync(tenantId.Value, id);
            return SuccessResponse(taxCode, "Tax code reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
