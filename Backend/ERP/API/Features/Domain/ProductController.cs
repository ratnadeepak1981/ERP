using API.Security.Authorization;
using Domain.Features.MasterData.Product;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Product;

[ApiController]
[Route("api/products")]
[RequirePermission("PRODUCT.VIEW")]
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

    [HttpPost]
    [RequirePermission("PRODUCT.CREATE")]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request)
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

        try
        {
            var product =
                await _productService.CreateProductAsync(
                    tenantId.Value,
                    request);

            return CreatedAtAction(
                nameof(GetProduct),
                new { id = product.ProductId },
                product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("PRODUCT.UPDATE")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request)
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

        try
        {
            var product = await _productService.UpdateProductAsync(
                tenantId.Value,
                id,
                request);

            return Ok(product);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Product not found."
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                status = "FAIL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                status = "FAIL",
                message = ex.Message
            });
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
            return Unauthorized(new
            {
                status = "FAIL",
                message = "Tenant identity is missing or invalid."
            });
        }

        try
        {
            await _productService.DeactivateProductAsync(
                tenantId.Value,
                id);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                status = "FAIL",
                message = "Product not found."
            });
        }
    }
}