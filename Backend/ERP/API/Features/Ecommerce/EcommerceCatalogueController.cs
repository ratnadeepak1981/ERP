using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Common.TenantResolution;
using ERP.Application.Features.Ecommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Features.Ecommerce;

/// <summary>
/// Public E-commerce Catalogue API.
/// Exposes strictly published products for the specified Selling Company.
/// Completely masks internal cost, valuation, supplier, and margin details.
/// </summary>
[ApiController]
[Route("api/ecommerce/catalogue")]
[AllowAnonymous]
public class EcommerceCatalogueController : BaseApiController
{
    private readonly IEcommerceProductService _ecommerceService;
    private readonly ITenantContext _tenantContext;

    public EcommerceCatalogueController(
        IEcommerceProductService ecommerceService,
        ITenantContext tenantContext)
    {
        _ecommerceService = ecommerceService ?? throw new ArgumentNullException(nameof(ecommerceService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    [HttpGet("{companyId:guid}/products")]
    public async Task<IActionResult> GetPublicCatalogue(
        Guid companyId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        var filter = new PublicCatalogueFilter
        {
            SearchTerm = searchTerm,
            CategoryId = categoryId,
            BrandId = brandId
        };

        try
        {
            var result = await _ecommerceService.GetPublicCatalogueAsync(
                companyId,
                filter,
                _tenantContext.TenantId,
                ct);

            return SuccessResponse(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{companyId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> GetPublicProductDetail(
        Guid companyId,
        Guid productId,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            return BadRequestResponse("Valid CompanyId is required.");

        if (productId == Guid.Empty)
            return BadRequestResponse("Valid ProductId is required.");

        try
        {
            var result = await _ecommerceService.GetPublicProductDetailAsync(
                companyId,
                productId,
                _tenantContext.TenantId,
                ct);

            return SuccessResponse(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
