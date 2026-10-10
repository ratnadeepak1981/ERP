using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.MasterData.Address;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Contact;

[ApiController]
[Route("api/contacts")]
[RequirePermission("CONTACT.VIEW")]
public class ContactController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly ICurrentUserContext _currentUserContext;

    public ContactController(
        IAddressService addressService,
        ICurrentUserContext currentUserContext)
    {
        _addressService = addressService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var contacts = await _addressService.GetContactsAsync(tenantId.Value);
        return Ok(contacts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetContact(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var contact = await _addressService.GetContactAsync(tenantId.Value, id);
        if (contact == null)
            return NotFound(new { status = "FAIL", message = "Contact not found." });

        return Ok(contact);
    }

    [HttpPost]
    [RequirePermission("CONTACT.CREATE")]
    public async Task<IActionResult> CreateContact([FromBody] CreateContactRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var contact = await _addressService.CreateContactAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetContact), new { id = contact.ContactId }, contact);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CONTACT.UPDATE")]
    public async Task<IActionResult> UpdateContact(Guid id, [FromBody] UpdateContactRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var contact = await _addressService.UpdateContactAsync(tenantId.Value, id, request);
            return Ok(contact);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Contact not found." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CONTACT.DELETE")]
    public async Task<IActionResult> DeactivateContact(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _addressService.DeactivateContactAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Contact not found." });
        }
    }

    // Contact Types
    [HttpGet("types")]
    public async Task<IActionResult> GetContactTypes()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var types = await _addressService.GetContactTypesAsync(tenantId.Value);
        return Ok(types);
    }

    [HttpPost("types")]
    [RequirePermission("CONTACT.MANAGE")]
    public async Task<IActionResult> CreateContactType([FromBody] CreateContactTypeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var type = await _addressService.CreateContactTypeAsync(tenantId.Value, request);
            return Ok(type);
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
}
