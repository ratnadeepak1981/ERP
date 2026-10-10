using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Supplier;

[ApiController]
[Route("api/suppliers")]
[RequirePermission("SUPPLIER.VIEW")]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserContext _currentUserContext;

    public SupplierController(
        ISupplierService supplierService,
        ICurrentUserContext currentUserContext)
    {
        _supplierService = supplierService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var suppliers = await _supplierService.GetSuppliersAsync(tenantId.Value);
        return Ok(suppliers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSupplier(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var supplier = await _supplierService.GetSupplierAsync(tenantId.Value, id);
        if (supplier == null)
            return NotFound(new { status = "FAIL", message = "Supplier not found." });

        return Ok(supplier);
    }

    [HttpPost]
    [RequirePermission("SUPPLIER.CREATE")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var supplier = await _supplierService.CreateSupplierAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.SupplierId }, supplier);
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
    [RequirePermission("SUPPLIER.UPDATE")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] UpdateSupplierRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var supplier = await _supplierService.UpdateSupplierAsync(tenantId.Value, id, request);
            return Ok(supplier);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier not found." });
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
    [RequirePermission("SUPPLIER.DELETE")]
    public async Task<IActionResult> DeactivateSupplier(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _supplierService.DeactivateSupplierAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier not found." });
        }
    }

    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var addresses = await _supplierService.GetAddressesAsync(tenantId.Value, id);
        return Ok(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] SupplierAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var assoc = await _supplierService.LinkAddressAsync(tenantId.Value, id, request);
            return Ok(assoc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier not found." });
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

    [HttpPut("{id:guid}/addresses/{addressAssocId:guid}/default")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> SetDefaultAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _supplierService.SetDefaultAddressAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier or address association not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/addresses/{addressAssocId:guid}")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> RemoveAddress(Guid id, Guid addressAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _supplierService.RemoveAddressLinkAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier address association not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> GetContacts(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var contacts = await _supplierService.GetContactsAsync(tenantId.Value, id);
        return Ok(contacts);
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> LinkContact(Guid id, [FromBody] SupplierContactLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var assoc = await _supplierService.LinkContactAsync(tenantId.Value, id, request);
            return Ok(assoc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier not found." });
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

    [HttpPut("{id:guid}/contacts/{contactAssocId:guid}/primary")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> SetPrimaryContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _supplierService.SetPrimaryContactAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier or contact association not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/contacts/{contactAssocId:guid}")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> RemoveContact(Guid id, Guid contactAssocId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _supplierService.RemoveContactLinkAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Supplier contact association not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }
}
