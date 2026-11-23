using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using ERP.Application.Features.Ecommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Ecommerce;

/// <summary>
/// Selling-Company-scoped employee API for managing which existing ERP Product Master records
/// are displayed in the company's public E-commerce catalogue.
/// </summary>
[ApiController]
[Route("api/ecommerce/products/publications")]
[Authorize]
public class EcommerceProductController : BaseApiController
{
    private readonly IEcommerceProductService _ecommerceService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserAccessService _userAccessService;

    public EcommerceProductController(
        IEcommerceProductService ecommerceService,
        ICurrentUserContext currentUserContext,
        IUserAccessService userAccessService)
    {
        _ecommerceService = ecommerceService ?? throw new ArgumentNullException(nameof(ecommerceService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _userAccessService = userAccessService ?? throw new ArgumentNullException(nameof(userAccessService));
    }

    [HttpGet]
    [RequirePermission("ECOMMERCE_PRODUCT.VIEW")]
    public async Task<IActionResult> GetPublications(
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.GetCompanyPublicationsAsync(tenantId, companyId, ct);
            return SuccessResponse(result);
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

    [HttpGet("{id:guid}")]
    [RequirePermission("ECOMMERCE_PRODUCT.VIEW")]
    public async Task<IActionResult> GetPublicationById(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.GetPublicationByIdAsync(tenantId, companyId, id, ct);
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

    [HttpPost]
    [RequirePermission("ECOMMERCE_PRODUCT.CREATE")]
    public async Task<IActionResult> CreatePublication(
        [FromBody] CreateEcommerceProductRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return BadRequestResponse("Request payload is required.");

        var (authError, tenantId) = await ValidateScopeAccessAsync(request.CompanyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.CreatePublicationAsync(tenantId, request.CompanyId, request, ct);
            return CreatedResponse(result, "E-commerce publication record created successfully.");
        }
        catch (ArgumentException ex)
        {
            return BadRequestResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("ECOMMERCE_PRODUCT.EDIT")]
    public async Task<IActionResult> UpdatePublication(
        Guid id,
        [FromQuery] Guid companyId,
        [FromBody] UpdateEcommerceProductRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return BadRequestResponse("Request payload is required.");

        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.UpdatePublicationAsync(tenantId, companyId, id, request, ct);
            return SuccessResponse(result, "E-commerce publication record updated successfully.");
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

    [HttpPut("{id:guid}/publish")]
    [RequirePermission("ECOMMERCE_PRODUCT.PUBLISH")]
    public async Task<IActionResult> PublishProduct(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.PublishAsync(tenantId, companyId, id, ct);
            return SuccessResponse(result, "Product published successfully to company e-commerce catalogue.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse(ex.Message);
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

    [HttpPut("{id:guid}/unpublish")]
    [RequirePermission("ECOMMERCE_PRODUCT.UNPUBLISH")]
    public async Task<IActionResult> UnpublishProduct(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var result = await _ecommerceService.UnpublishAsync(tenantId, companyId, id, ct);
            return SuccessResponse(result, "Product unpublished successfully from public e-commerce visibility.");
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

    [HttpDelete("{id:guid}")]
    [RequirePermission("ECOMMERCE_PRODUCT.DELETE")]
    public async Task<IActionResult> DeletePublication(
        Guid id,
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            await _ecommerceService.DeletePublicationAsync(tenantId, companyId, id, ct);
            return SuccessResponse("E-commerce publication record deleted successfully.");
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

    [HttpPost("backfill")]
    [RequirePermission("ECOMMERCE_PRODUCT.CREATE")]
    public async Task<IActionResult> BackfillPublications(
        [FromQuery] Guid companyId,
        CancellationToken ct = default)
    {
        var (authError, tenantId) = await ValidateScopeAccessAsync(companyId);
        if (authError != null)
            return authError;

        try
        {
            var count = await _ecommerceService.BackfillCompanyPublicationsAsync(tenantId, companyId, ct);
            return SuccessResponse(new { backfilledCount = count }, $"Successfully backfilled {count} products into publication.");
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

    private async Task<(IActionResult? Error, Guid TenantId)> ValidateScopeAccessAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return (BadRequestResponse("CompanyId is required."), Guid.Empty);
        }

        if (_currentUserContext.IsPlatformUser)
        {
            return (Forbid(), Guid.Empty);
        }

        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
        {
            return (UnauthorizedResponse("Tenant identity is missing or invalid."), Guid.Empty);
        }

        // Rule 12: Never trust a CompanyId supplied by the client without checking user's company scope
        var canAccessCompany = await _userAccessService.CanAccessCompanyAsync(
            _currentUserContext.UserId,
            tenantId.Value,
            companyId);

        if (!canAccessCompany)
        {
            return (Forbid(), Guid.Empty);
        }

        return (null, tenantId.Value);
    }
}
