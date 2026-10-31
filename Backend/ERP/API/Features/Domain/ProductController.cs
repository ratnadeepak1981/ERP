using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Product;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Product;

[ApiController]
[Route("api/products")]
[RequirePermission("PRODUCT.VIEW")]
public class ProductController : BaseApiController
{
    private readonly IProductService _productService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.Product.Product> _sortMapping =
        new SortMapping<global::Domain.Features.MasterData.Product.Product>()
            .Add("productCode", p => p.ProductCode, isDefault: true)
            .Add("productName", p => p.ProductName)
            .Add("unitOfMeasure", p => p.UnitOfMeasure)
            .Add("brandId", p => (object?)p.BrandId ?? Guid.Empty)
            .Add("createdAt", p => p.CreatedAt)
            .SetSecondary(p => p.ProductId);

    public ProductController(
        IProductService productService,
        ICurrentUserContext currentUserContext)
    {
        _productService = productService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] Guid? brandId = null)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var products = await _productService.GetProductsAsync(tenantId.Value, brandId);
        return PaginateAndSort(products, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        var product = await _productService.GetProductAsync(tenantId.Value, id);
        if (product == null)
        {
            return NotFoundResponse("Product not found.");
        }

        return SuccessResponse(product);
    }

    [HttpPost]
    [RequirePermission("PRODUCT.CREATE")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        try
        {
            var product = await _productService.CreateProductAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetProduct), new { id = product.ProductId }, product);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        try
        {
            var product = await _productService.UpdateProductAsync(tenantId.Value, id, request);
            return SuccessResponse(product);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("PRODUCT.DELETE")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return UnauthorizedResponse("Tenant identity is missing or invalid.");
        }

        try
        {
            await _productService.DeactivateProductAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> ReactivateProduct(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var product = await _productService.ReactivateProductAsync(tenantId.Value, id);
            return SuccessResponse(product, "Product reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}