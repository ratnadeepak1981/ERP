using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Supplier;

[ApiController]
[Route("api/suppliers")]
[RequirePermission("SUPPLIER.VIEW")]
public class SupplierController : BaseApiController
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::ERP.Domain.Features.MasterData.Supplier.Supplier> _sortMapping =
        new SortMapping<global::ERP.Domain.Features.MasterData.Supplier.Supplier>()
            .Add("supplierCode", s => s.SupplierCode, isDefault: true)
            .Add("supplierName", s => s.SupplierName)
            .Add("city", s => s.City ?? "")
            .SetSecondary(s => s.SupplierId);

    public SupplierController(
        ISupplierService supplierService,
        ICurrentUserContext currentUserContext)
    {
        _supplierService = supplierService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var suppliers = await _supplierService.GetSuppliersAsync(tenantId.Value);
        return PaginateAndSort(suppliers, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSupplier(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var supplier = await _supplierService.GetSupplierAsync(tenantId.Value, id);
        if (supplier == null)
            return NotFoundResponse("Supplier not found.");

        return SuccessResponse(supplier);
    }

    [HttpPost]
    [RequirePermission("SUPPLIER.CREATE")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var supplier = await _supplierService.CreateSupplierAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetSupplier), new { id = supplier.SupplierId }, supplier);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("SUPPLIER.EDIT")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var supplier = await _supplierService.UpdateSupplierAsync(tenantId.Value, id, request);
            return SuccessResponse(supplier);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("SUPPLIER.DELETE")]
    public async Task<IActionResult> DeactivateSupplier(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _supplierService.DeactivateSupplierAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("SUPPLIER.EDIT")]
    public async Task<IActionResult> ReactivateSupplier(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var supplier = await _supplierService.ReactivateSupplierAsync(tenantId.Value, id);
            return SuccessResponse(supplier, "Supplier reactivated successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var addresses = await _supplierService.GetAddressesAsync(tenantId.Value, id);
        return SuccessResponse(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] SupplierAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var assoc = await _supplierService.LinkAddressAsync(tenantId.Value, id, request);
            return SuccessResponse(assoc, "Supplier address linked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/addresses/{addressAssocId:guid}/default")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> SetDefaultAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _supplierService.SetDefaultAddressAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/addresses/{addressAssocId:guid}")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> RemoveAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _supplierService.RemoveAddressLinkAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> GetContacts(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var contacts = await _supplierService.GetContactsAsync(tenantId.Value, id);
        return SuccessResponse(contacts);
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> LinkContact(Guid id, [FromBody] SupplierContactLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var assoc = await _supplierService.LinkContactAsync(tenantId.Value, id, request);
            return SuccessResponse(assoc, "Supplier contact linked successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}/contacts/{contactAssocId:guid}/primary")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> SetPrimaryContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _supplierService.SetPrimaryContactAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}/contacts/{contactAssocId:guid}")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> RemoveContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _supplierService.RemoveContactLinkAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
