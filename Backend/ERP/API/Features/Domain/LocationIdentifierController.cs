using System;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/warehouses/{warehouseId:guid}/locations/{locationId:guid}/identifiers")]
public class LocationIdentifierController : BaseApiController
{
    private readonly ILocationIdentifierService _identifierService;
    private readonly ICurrentUserContext _currentUserContext;

    public LocationIdentifierController(
        ILocationIdentifierService identifierService,
        ICurrentUserContext currentUserContext)
    {
        _identifierService = identifierService ?? throw new ArgumentNullException(nameof(identifierService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    [HttpGet]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetIdentifiers(Guid warehouseId, Guid locationId, [FromQuery] bool includeInactive = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifiers = await _identifierService.GetIdentifiersAsync(tenantId.Value, warehouseId, locationId, includeInactive);
            return SuccessResponse(identifiers);
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

    [HttpGet("{identifierId:guid}")]
    [RequirePermission("LOCATION.VIEW")]
    public async Task<IActionResult> GetIdentifier(Guid warehouseId, Guid locationId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.GetIdentifierByIdAsync(tenantId.Value, warehouseId, locationId, identifierId);
            if (identifier == null)
            {
                return NotFoundResponse("Location identifier not found.");
            }
            return SuccessResponse(identifier);
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

    [HttpPost]
    [RequirePermission("LOCATION.EDIT")]
    public async Task<IActionResult> AddIdentifier(Guid warehouseId, Guid locationId, [FromBody] CreateLocationIdentifierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.AddIdentifierAsync(tenantId.Value, warehouseId, locationId, request);
            return CreatedResponse(identifier, "Location identifier registered successfully.");
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
    [RequirePermission("LOCATION.EDIT")]
    public async Task<IActionResult> GenerateInternalIdentifier(Guid warehouseId, Guid locationId, [FromBody] GenerateInternalLocationIdentifierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.GenerateInternalIdentifierAsync(tenantId.Value, warehouseId, locationId, request);
            return CreatedResponse(identifier, "Internal location identifier generated successfully.");
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
    [RequirePermission("LOCATION.EDIT")]
    public async Task<IActionResult> SetPrimary(Guid warehouseId, Guid locationId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.SetPrimaryAsync(tenantId.Value, warehouseId, locationId, identifierId);
            return SuccessResponse(identifier, "Location identifier promoted to primary successfully.");
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
    [RequirePermission("LOCATION.EDIT")]
    public async Task<IActionResult> DeactivateIdentifier(Guid warehouseId, Guid locationId, Guid identifierId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _identifierService.DeactivateIdentifierAsync(tenantId.Value, warehouseId, locationId, identifierId);
            return SuccessResponse<object?>(null, "Location identifier deactivated successfully.");
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

    [HttpPost("{identifierId:guid}/reactivate")]
    [RequirePermission("LOCATION.EDIT")]
    public async Task<IActionResult> ReactivateIdentifier(Guid warehouseId, Guid locationId, Guid identifierId, [FromQuery] bool isPrimary = false)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue) return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var identifier = await _identifierService.ReactivateIdentifierAsync(tenantId.Value, warehouseId, locationId, identifierId, isPrimary);
            return SuccessResponse(identifier, "Location identifier reactivated successfully.");
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
