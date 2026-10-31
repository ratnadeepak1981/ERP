using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Domain.Features.MasterData.Product;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/products")]
public class ProductPackagingController : BaseApiController
{
    private readonly IProductPackagingService _packagingService;
    private readonly ICurrentUserContext _currentUserContext;

    public ProductPackagingController(
        IProductPackagingService packagingService,
        ICurrentUserContext currentUserContext)
    {
        _packagingService = packagingService ?? throw new ArgumentNullException(nameof(packagingService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpGet("{productId:guid}/packagings")]
    [RequirePermission("PRODUCT.VIEW")]
    public async Task<IActionResult> GetPackagingsByProduct(Guid productId, [FromQuery] bool includeInactive = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var packagings = await _packagingService.GetPackagingsByProductAsync(tenantId.Value, productId, includeInactive);
            return SuccessResponse(packagings);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("packagings")]
    [RequirePermission("PRODUCT.VIEW")]
    public async Task<IActionResult> GetAllPackagings(
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] bool includeInactive = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var packagings = await _packagingService.GetPackagingsAsync(tenantId.Value, productId, companyId, includeInactive);
            return SuccessResponse(packagings);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("packagings/{packagingId:guid}")]
    [RequirePermission("PRODUCT.VIEW")]
    public async Task<IActionResult> GetPackagingById(Guid packagingId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var packaging = await _packagingService.GetPackagingByIdAsync(tenantId.Value, packagingId);
            if (packaging == null) return NotFoundResponse("Product packaging definition not found.");
            return SuccessResponse(packaging);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{productId:guid}/packagings")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> CreatePackaging(Guid productId, [FromBody] CreateProductPackagingRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        if (request == null) return BadRequestResponse("Request body cannot be null.");
        request.ProductId = productId;

        try
        {
            var packaging = await _packagingService.CreatePackagingAsync(tenantId.Value, request);
            return CreatedResponse(packaging, "Product packaging created successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("packagings/{packagingId:guid}")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> UpdatePackaging(Guid packagingId, [FromBody] UpdateProductPackagingRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        if (request == null) return BadRequestResponse("Request body cannot be null.");

        try
        {
            var packaging = await _packagingService.UpdatePackagingAsync(tenantId.Value, packagingId, request);
            return SuccessResponse(packaging, "Product packaging updated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("packagings/{packagingId:guid}")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> DeactivatePackaging(Guid packagingId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _packagingService.DeactivatePackagingAsync(tenantId.Value, packagingId);
            return SuccessResponse<object?>(null, "Product packaging deactivated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("packagings/{packagingId:guid}/reactivate")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> ReactivatePackaging(Guid packagingId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var packaging = await _packagingService.ReactivatePackagingAsync(tenantId.Value, packagingId);
            return SuccessResponse(packaging, "Product packaging reactivated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
