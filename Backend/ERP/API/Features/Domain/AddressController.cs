using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.Address;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Address;

[ApiController]
[Route("api/addresses")]
[RequirePermission("ADDRESS.VIEW")]
public class AddressController : BaseApiController
{
    private readonly IAddressService _addressService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<global::Domain.Features.MasterData.Address.Address> _sortMapping = new SortMapping<global::Domain.Features.MasterData.Address.Address>()
        .Add("addressName", a => a.AddressName, isDefault: true)
        .Add("city", a => a.City)
        .Add("postalCode", a => a.PostalCode ?? "")
        .SetSecondary(a => a.AddressId);

    public AddressController(
        IAddressService addressService,
        ICurrentUserContext currentUserContext)
    {
        _addressService = addressService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAddresses(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var addresses = await _addressService.GetAddressesAsync(tenantId.Value);
        return PaginateAndSort(addresses, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAddress(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var address = await _addressService.GetAddressAsync(tenantId.Value, id);
        if (address == null)
            return NotFoundResponse("Address not found.");

        return SuccessResponse(address);
    }

    [HttpPost]
    [RequirePermission("ADDRESS.CREATE")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var address = await _addressService.CreateAddressAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetAddress), new { id = address.AddressId }, address);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("ADDRESS.UPDATE")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var address = await _addressService.UpdateAddressAsync(tenantId.Value, id, request);
            return SuccessResponse(address);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/resolve-country")]
    [RequirePermission("ADDRESS.UPDATE")]
    public async Task<IActionResult> ResolveCountry(Guid id, [FromBody] Guid countryId)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var address = await _addressService.ResolveCountryAsync(tenantId.Value, id, countryId);
            return SuccessResponse(address);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("ADDRESS.DELETE")]
    public async Task<IActionResult> DeactivateAddress(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _addressService.DeactivateAddressAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    // Address Types
    [HttpGet("types")]
    public async Task<IActionResult> GetAddressTypes()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var types = await _addressService.GetAddressTypesAsync(tenantId.Value);
        return SuccessResponse(types);
    }

    [HttpPost("types")]
    [RequirePermission("ADDRESS.MANAGE")]
    public async Task<IActionResult> CreateAddressType([FromBody] CreateAddressTypeRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var type = await _addressService.CreateAddressTypeAsync(tenantId.Value, request);
            return SuccessResponse(type, "Address type created successfully.");
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
