using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Supplier;

[ApiController]
[Route("api/supplier-prices")]
[RequirePermission("SUPPLIER_PRICE.VIEW")]
public class SupplierProductPriceController : ControllerBase
{
    private readonly ISupplierProductPriceService _priceService;
    private readonly ICurrentUserContext _currentUserContext;

    public SupplierProductPriceController(
        ISupplierProductPriceService priceService,
        ICurrentUserContext currentUserContext)
    {
        _priceService = priceService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrices([FromQuery] Guid? supplierId, [FromQuery] Guid? productId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var prices = await _priceService.GetPricesAsync(tenantId.Value, supplierId, productId);
        return Ok(prices);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPrice(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var price = await _priceService.GetPriceAsync(tenantId.Value, id);
        if (price == null)
            return NotFound(new { status = "FAIL", message = "Supplier product price card not found." });

        return Ok(price);
    }

    [HttpPost]
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> CreatePrice([FromBody] CreateSupplierProductPriceRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var price = await _priceService.CreatePriceAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetPrice), new { id = price.SupplierProductPriceId }, price);
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
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdateSupplierProductPriceRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var price = await _priceService.UpdatePriceAsync(tenantId.Value, id, request);
            return Ok(price);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier product price card not found." });
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
    [RequirePermission("SUPPLIER_PRICE.MANAGE")]
    public async Task<IActionResult> DeactivatePrice(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _priceService.DeactivatePriceAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier product price card not found." });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActivePrice([FromQuery] Guid supplierId, [FromQuery] Guid productId, [FromQuery] DateTime? asOfDate)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var price = await _priceService.GetActivePriceAsync(tenantId.Value, supplierId, productId, asOfDate);
        if (price == null)
            return NotFound(new { status = "FAIL", message = "No active price found for this supplier and product." });

        return Ok(price);
    }
}
