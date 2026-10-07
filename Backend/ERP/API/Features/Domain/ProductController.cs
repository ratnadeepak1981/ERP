using Domain.Features.MasterData.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Product;

[ApiController]
[Route("api/products")]
[Authorize(Policy = "PRODUCT_VIEW")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ICurrentUserContext _currentUserContext;

    public ProductController(
        IProductService productService,
        ICurrentUserContext currentUserContext)
    {
        _productService = productService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            });
        }

        var products =
            await _productService.GetProductsAsync(
                tenantId.Value);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(
        Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
        {
            return Forbid();
        }

        var tenantId = _currentUserContext.TenantId;

        if (!tenantId.HasValue)
        {
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            });
        }

        var product =
            await _productService.GetProductAsync(
                tenantId.Value,
                id);

        if (product == null)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Product not found."
            });
        }

        return Ok(product);
    }
}