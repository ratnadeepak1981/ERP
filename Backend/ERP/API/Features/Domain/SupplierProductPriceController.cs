using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Supplier;

[ApiController]
[Route("api/supplier-prices")]
[RequirePermission("SUPPLIER_PRICE.VIEW")]
public class SupplierProductPriceController : BaseApiController
{
    private readonly ISupplierProductPriceService _priceService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<SupplierProductPrice> _sortMapping = new SortMapping<SupplierProductPrice>()
        .Add("unitPrice", p => p.UnitPrice, isDefault: true)
        .Add("effectiveFrom", p => p.EffectiveFrom)
        .SetSecondary(p => p.SupplierProductPriceId);

    public SupplierProductPriceController(
        ISupplierProductPriceService priceService,
        ICurrentUserContext currentUserContext)
    {
        _priceService = priceService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrices(
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? productId,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var prices = await _priceService.GetPricesAsync(tenantId.Value, supplierId, productId);
        return PaginateAndSort(prices, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPrice(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var price = await _priceService.GetPriceAsync(tenantId.Value, id);
        if (price == null)
            return NotFoundResponse("Supplier product price card not found.");

        return SuccessResponse(price);
    }

    [HttpPost]
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> CreatePrice([FromBody] CreateSupplierProductPriceRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var price = await _priceService.CreatePriceAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetPrice), new { id = price.SupplierProductPriceId }, price);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdateSupplierProductPriceRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var price = await _priceService.UpdatePriceAsync(tenantId.Value, id, request);
            return SuccessResponse(price);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> DeactivatePrice(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _priceService.DeactivatePriceAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActivePrice([FromQuery] Guid supplierId, [FromQuery] Guid productId, [FromQuery] DateTime? asOfDate)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var price = await _priceService.GetActivePriceAsync(tenantId.Value, supplierId, productId, asOfDate);
        if (price == null)
            return NotFoundResponse("No active price found for this supplier and product.");

        return SuccessResponse(price);
    }
}
