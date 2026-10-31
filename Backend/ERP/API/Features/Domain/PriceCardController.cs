using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Common;
using API.Common.Sorting;
using API.Security.Authorization;
using Domain.Features.MasterData.PriceCard;
using Microsoft.AspNetCore.Mvc;
using Security.Interfaces;

namespace API.Features.MasterData.PriceCard;

[ApiController]
[Route("api/master-data/price-cards")]
[RequirePermission("PRICE_CARD.VIEW")]
public class PriceCardController : BaseApiController
{
    private readonly IPriceCardService _priceCardService;
    private readonly ICurrentUserContext _currentUserContext;

    private static readonly SortMapping<PriceCardDto> _sortMapping =
        new SortMapping<PriceCardDto>()
            .Add("code", p => p.PriceCardCode, isDefault: true)
            .Add("priceCardCode", p => p.PriceCardCode)
            .Add("name", p => p.PriceCardName)
            .Add("priceCardName", p => p.PriceCardName)
            .Add("effectiveFrom", p => p.EffectiveFrom)
            .Add("effectiveTo", p => (object?)p.EffectiveTo ?? DateTime.MaxValue)
            .Add("type", p => p.PriceCardType)
            .Add("priceCardType", p => p.PriceCardType)
            .Add("currency", p => p.Currency)
            .Add("isActive", p => p.IsActive)
            .SetSecondary(p => p.PriceCardId);

    public PriceCardController(
        IPriceCardService priceCardService,
        ICurrentUserContext currentUserContext)
    {
        _priceCardService = priceCardService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetPriceCards(
        [FromQuery] PriceCardType? type = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var cards = await _priceCardService.GetPriceCardsAsync(tenantId.Value, type, search, isActive);
        return PaginateAndSort(cards, page, pageSize, sortBy, sortDirection, _sortMapping);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPriceCard(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        var card = await _priceCardService.GetPriceCardAsync(tenantId.Value, id);
        if (card == null)
            return NotFoundResponse("Price card not found.");

        return SuccessResponse(card);
    }

    [HttpPost]
    [RequirePermission("PRICE_CARD.CREATE")]
    public async Task<IActionResult> CreatePriceCard([FromBody] CreatePriceCardRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var card = await _priceCardService.CreatePriceCardAsync(tenantId.Value, request);
            return CreatedResponse(nameof(GetPriceCard), new { id = card.PriceCardId }, card);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("PRICE_CARD.EDIT")]
    public async Task<IActionResult> UpdatePriceCard(Guid id, [FromBody] UpdatePriceCardRequest request)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            var card = await _priceCardService.UpdatePriceCardAsync(tenantId.Value, id, request);
            return SuccessResponse(card);
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPatch("{id:guid}/deactivate")]
    [RequirePermission("PRICE_CARD.MANAGE")]
    public async Task<IActionResult> DeactivatePriceCard(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _priceCardService.DeactivatePriceCardAsync(tenantId.Value, id);
            return SuccessResponse(new { message = "Price card deactivated successfully." });
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }

    [HttpPatch("{id:guid}/reactivate")]
    [RequirePermission("PRICE_CARD.MANAGE")]
    public async Task<IActionResult> ReactivatePriceCard(Guid id)
    {
        if (_currentUserContext.IsPlatformUser) return Forbid();
        var tenantId = _currentUserContext.TenantId;
        if (!tenantId.HasValue)
            return UnauthorizedResponse("Tenant identity is missing or invalid.");

        try
        {
            await _priceCardService.ReactivatePriceCardAsync(tenantId.Value, id);
            return SuccessResponse(new { message = "Price card reactivated successfully." });
        }
        catch (Exception ex)
        {
            return HandleDomainException(ex);
        }
    }
}
