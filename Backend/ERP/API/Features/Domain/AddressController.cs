using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.MasterData.Address;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Address;

[ApiController]
[Route("api/addresses")]
[RequirePermission("ADDRESS.VIEW")]
public class AddressController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly ICurrentUserContext _currentUserContext;

    public AddressController(
        IAddressService addressService,
        ICurrentUserContext currentUserContext)
    {
        _addressService = addressService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAddresses()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var addresses = await _addressService.GetAddressesAsync(tenantId.Value);
        return Ok(addresses);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAddress(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var address = await _addressService.GetAddressAsync(tenantId.Value, id);
        if (address == null)
            return NotFound(new { status = "FAIL", message = "Address not found." });

        return Ok(address);
    }

    [HttpPost]
    [RequirePermission("ADDRESS.CREATE")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var address = await _addressService.CreateAddressAsync(tenantId.Value, request);
            return CreatedAtAction(nameof(GetAddress), new { id = address.AddressId }, address);
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
    [RequirePermission("ADDRESS.UPDATE")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var address = await _addressService.UpdateAddressAsync(tenantId.Value, id, request);
            return Ok(address);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Address not found." });
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

    [HttpPost("{id:guid}/resolve-country")]
    [RequirePermission("ADDRESS.UPDATE")]
    public async Task<IActionResult> ResolveCountry(Guid id, [FromBody] Guid countryId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var address = await _addressService.ResolveCountryAsync(tenantId.Value, id, countryId);
            return Ok(address);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Address not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { status = "FAIL", message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("ADDRESS.DELETE")]
    public async Task<IActionResult> DeactivateAddress(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            await _addressService.DeactivateAddressAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { status = "FAIL", message = "Address not found." });
        }
    }

    // Address Types
    [HttpGet("types")]
    public async Task<IActionResult> GetAddressTypes()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var types = await _addressService.GetAddressTypesAsync(tenantId.Value);
        return Ok(types);
    }

    [HttpPost("types")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> CreateAddressType([FromBody] CreateAddressTypeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        try
        {
            var type = await _addressService.CreateAddressTypeAsync(tenantId.Value, request);
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
