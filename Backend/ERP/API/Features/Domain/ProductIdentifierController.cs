using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Domain.Features.MasterData.Product;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/products/{productId:guid}/identifiers")]
public class ProductIdentifierController : BaseApiController
{
    private readonly IProductIdentifierService _identifierService;
    private readonly ICurrentUserContext _currentUserContext;

    public ProductIdentifierController(
        IProductIdentifierService identifierService,
        ICurrentUserContext currentUserContext)
    {
        _identifierService = identifierService ?? throw new ArgumentNullException(nameof(identifierService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpGet]
    [RequirePermission("PRODUCT.VIEW")]
    public async Task<IActionResult> GetIdentifiers(Guid productId, [FromQuery] bool includeInactive = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifiers = await _identifierService.GetIdentifiersAsync(tenantId.Value, productId, includeInactive);
            return SuccessResponse(identifiers);
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

    [HttpGet("{identifierId:guid}")]
    [RequirePermission("PRODUCT.VIEW")]
    public async Task<IActionResult> GetIdentifier(Guid productId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.GetIdentifierByIdAsync(tenantId.Value, productId, identifierId);
            if (identifier == null)
            {
                return NotFoundResponse("Product identifier not found.");
            }
            return SuccessResponse(identifier);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> AddIdentifier(Guid productId, [FromBody] CreateProductIdentifierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.AddIdentifierAsync(tenantId.Value, productId, request);
            return CreatedResponse(identifier, "Product identifier registered successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
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

    [HttpPost("generate")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> GenerateInternalIdentifier(Guid productId, [FromBody] GenerateInternalProductIdentifierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.GenerateInternalIdentifierAsync(tenantId.Value, productId, request);
            return CreatedResponse(identifier, "Internal product identifier generated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
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

    [HttpPut("{identifierId:guid}/primary")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> SetPrimary(Guid productId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.SetPrimaryAsync(tenantId.Value, productId, identifierId);
            return SuccessResponse(identifier, "Product identifier promoted to primary successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
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

    [HttpDelete("{identifierId:guid}")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> DeactivateIdentifier(Guid productId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _identifierService.DeactivateIdentifierAsync(tenantId.Value, productId, identifierId);
            return SuccessResponse<object?>(null, "Product identifier deactivated successfully.");
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

    [HttpPost("{identifierId:guid}/reactivate")]
    [RequirePermission("PRODUCT.EDIT")]
    public async Task<IActionResult> ReactivateIdentifier(Guid productId, Guid identifierId, [FromQuery] bool isPrimary = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.ReactivateIdentifierAsync(tenantId.Value, productId, identifierId, isPrimary);
            return SuccessResponse(identifier, "Product identifier reactivated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
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
}
