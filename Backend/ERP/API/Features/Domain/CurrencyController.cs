using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Security.Authorization;
using Domain.Features.MasterData.Currency;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/currencies")]
[RequirePermission("CURRENCY.VIEW")]
public class CurrencyController : BaseApiController
{
    private readonly ICurrencyService _currencyService;
    private readonly ICurrentUserContext _currentUserContext;

    public CurrencyController(
        ICurrencyService currencyService,
        ICurrentUserContext currentUserContext)
    {
        _currencyService = currencyService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrencies()
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var currencies = await _currencyService.GetCurrenciesAsync(tenantId.Value);
        return SuccessResponse(currencies);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCurrency(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var currency = await _currencyService.GetCurrencyByIdAsync(tenantId.Value, id);
        if (currency == null)
            return NotFoundResponse("Currency not found.");

        return SuccessResponse(currency);
    }

    [HttpPost]
    [RequirePermission("CURRENCY.CREATE")]
    public async Task<IActionResult> CreateCurrency([FromBody] CreateCurrencyRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var currency = await _currencyService.CreateCurrencyAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetCurrency), new { id = currency.CurrencyId }, currency);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CURRENCY.EDIT")]
    public async Task<IActionResult> UpdateCurrency(Guid id, [FromBody] UpdateCurrencyRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var currency = await _currencyService.UpdateCurrencyAsync(tenantId.Value, id, request);
            return SuccessResponse(currency);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CURRENCY.DELETE")]
    public async Task<IActionResult> DeactivateCurrency(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _currencyService.DeactivateCurrencyAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission("CURRENCY.EDIT")]
    public async Task<IActionResult> ActivateCurrency(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _currencyService.ActivateCurrencyAsync(tenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
