using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Customer;

[ApiController]
[Route("api/customers")]
[RequirePermission("CUSTOMER.VIEW")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICurrentUserContext _currentUserContext;

    public CustomerController(
        ICustomerService customerService,
        ICurrentUserContext currentUserContext)
    {
        _customerService = customerService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var customers = await _customerService.GetCustomersAsync(tenantId.Value);
        return Ok(customers);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var customer = await _customerService.GetCustomerAsync(tenantId.Value, id);
        if (customer == null)
            return NotFound(new { status = "FAIL", message = "Customer not found." });

        return Ok(customer);
    }

    [HttpPost]
    [RequirePermission("CUSTOMER.CREATE")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var customer = await _customerService.CreateCustomerAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, customer);
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
    [RequirePermission("CUSTOMER.UPDATE")]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var customer = await _customerService.UpdateCustomerAsync(tenantId.Value, id, request);
            return Ok(customer);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer not found." });
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
    [RequirePermission("CUSTOMER.DELETE")]
    public async Task<IActionResult> DeactivateCustomer(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _customerService.DeactivateCustomerAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer not found." });
        }
    }

    [HttpGet("{id:guid}/addresses")]
    public async Task<IActionResult> GetAddresses(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var addresses = await _customerService.GetAddressesAsync(tenantId.Value, id);
        return Ok(addresses);
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> LinkAddress(Guid id, [FromBody] CustomerAddressLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var assoc = await _customerService.LinkAddressAsync(tenantId.Value, id, request);
            return Ok(assoc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer not found." });
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
            await _customerService.SetDefaultAddressAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer or address association not found." });
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
            await _customerService.RemoveAddressLinkAsync(tenantId.Value, id, addressAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer address association not found." });
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

        var contacts = await _customerService.GetContactsAsync(tenantId.Value, id);
        return Ok(contacts);
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> LinkContact(Guid id, [FromBody] CustomerContactLinkRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var assoc = await _customerService.LinkContactAsync(tenantId.Value, id, request);
            return Ok(assoc);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer not found." });
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
            await _customerService.SetPrimaryContactAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer or contact association not found." });
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
            await _customerService.RemoveContactLinkAsync(tenantId.Value, id, contactAssocId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Customer contact association not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }
}
