using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Security.Authorization;
using Domain.Features.MasterData.Address;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.Country;

[ApiController]
[Route("api/countries")]
[RequirePermission("COUNTRY.VIEW")]
public class CountryController : ControllerBase
{
    private readonly IAddressService _addressService;
    private readonly ICurrentUserContext _currentUserContext;

    public CountryController(
        IAddressService addressService,
        ICurrentUserContext currentUserContext)
    {
        _addressService = addressService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCountries()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var countries = await _addressService.GetCountriesAsync();
        return Ok(countries);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCountry(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return Unauthorized(new { status = "FAIL", message = "Tenant identity is missing or invalid." });

        var country = await _addressService.GetCountryByIdAsync(id);
        if (country == null)
            return NotFound(new { status = "FAIL", message = "Country not found." });

        return Ok(country);
    }
}
