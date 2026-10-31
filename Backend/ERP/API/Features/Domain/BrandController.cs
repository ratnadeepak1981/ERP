using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Brand;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Brand;

[ApiController]
[Route("api/brands")]
[RequirePermission("BRAND.VIEW")]
public class BrandController : BaseApiController
{
    private readonly IBrandService _brandService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.Brand.Brand> _sortMapping =
        new SortMapping<global::Domain.Features.MasterData.Brand.Brand>()
            .Add("brandCode", b => b.BrandCode, isDefault: true)
            .Add("brandName", b => b.BrandName)
            .SetSecondary(b => b.BrandId);

    public BrandController(
        IBrandService brandService,
        ICurrentUserContext currentUserContext)
    {
        _brandService = brandService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetBrands(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var brands = await _brandService.GetBrandsAsync(tenantId.Value);
        return PaginateAndSort(brands, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBrand(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var brand = await _brandService.GetBrandAsync(tenantId.Value, id);
        if (brand == null)
            return NotFoundResponse("Brand not found.");

        return SuccessResponse(brand);
    }

    [HttpPost]
    [RequirePermission("BRAND.CREATE")]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var brand = await _brandService.CreateBrandAsync(tenantId.Value, request);
            return CreatedResponse(brand, "Brand created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("BRAND.EDIT")]
    public async Task<IActionResult> UpdateBrand(Guid id, [FromBody] UpdateBrandRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var brand = await _brandService.UpdateBrandAsync(tenantId.Value, id, request);
            return SuccessResponse(brand);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("BRAND.DELETE")]
    public async Task<IActionResult> DeactivateBrand(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _brandService.DeactivateBrandAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("BRAND.EDIT")]
    public async Task<IActionResult> ReactivateBrand(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var brand = await _brandService.ReactivateBrandAsync(tenantId.Value, id);
            return SuccessResponse(brand, "Brand reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
