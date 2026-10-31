using Domain.Features.MasterData.Currency;
using Microsoft.AspNetCore.Mvc;
using API.Common;
using API.Security.Authorization;
using Security.Interfaces;

namespace API.Features.Domain;

[ApiController]
[Route("api/exchange-rates")]
[RequirePermission("CURRENCY.VIEW")]
public class ExchangeRateController : BaseApiController
{
    private readonly IExchangeRateService _service;
    private readonly ICurrentUserContext _currentUserContext;

    public ExchangeRateController(
        IExchangeRateService service,
        ICurrentUserContext currentUserContext)
    {
        _service = service;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetRates()
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        return SuccessResponse(await _service.GetRatesAsync(_currentUserContext.TenantId.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRate(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var rate = await _service.GetRateAsync(_currentUserContext.TenantId.Value, id);
        return rate == null ? NotFoundResponse("Exchange rate not found.") : SuccessResponse(rate);
    }

    [HttpPost]
    [RequirePermission("CURRENCY.CREATE")]
    public async Task<IActionResult> CreateRate([FromBody] CreateExchangeRateRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var rate = await _service.CreateRateAsync(_currentUserContext.TenantId.Value, request);
            return CreatedResponse(nameof(GetRate), new { id = rate.ExchangeRateId }, rate);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("CURRENCY.EDIT")]
    public async Task<IActionResult> UpdateRate(Guid id, [FromBody] UpdateExchangeRateRequest request)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            return SuccessResponse(await _service.UpdateRateAsync(_currentUserContext.TenantId.Value, id, request));
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("CURRENCY.EDIT")]
    public async Task<IActionResult> DeactivateRate(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _service.DeactivateRateAsync(_currentUserContext.TenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission("CURRENCY.EDIT")]
    public async Task<IActionResult> ActivateRate(Guid id)
    {
        if (_currentUserContext.IsPlatformUser)
            return Forbid();
        if (!_currentUserContext.TenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _service.ActivateRateAsync(_currentUserContext.TenantId.Value, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
