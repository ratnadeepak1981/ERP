using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.PriceCard;

public interface IPriceCardService
{
    Task<List<PriceCardDto>> GetPriceCardsAsync(Guid tenantId, PriceCardType? type = null, string? search = null, bool? isActive = null);
    Task<PriceCardDto?> GetPriceCardAsync(Guid tenantId, Guid priceCardId);
    Task<PriceCardDto> CreatePriceCardAsync(Guid tenantId, CreatePriceCardRequest request);
    Task<PriceCardDto> UpdatePriceCardAsync(Guid tenantId, Guid priceCardId, UpdatePriceCardRequest request);
    Task DeactivatePriceCardAsync(Guid tenantId, Guid priceCardId);
    Task ReactivatePriceCardAsync(Guid tenantId, Guid priceCardId);

    Task<List<PriceCardLineDto>> GetLinesAsync(Guid tenantId, Guid priceCardId);
    Task<PriceCardLineDto> AddLineAsync(Guid tenantId, Guid priceCardId, CreatePriceCardLineRequest request);
    Task<PriceCardLineDto> UpdateLineAsync(Guid tenantId, Guid priceCardId, Guid lineId, UpdatePriceCardLineRequest request);
    Task DeleteLineAsync(Guid tenantId, Guid priceCardId, Guid lineId);

    Task<PriceResolutionResult> GetApplicablePriceAsync(
        Guid tenantId,
        Guid priceCardId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? asOfDate = null);
}
