using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.PriceCard;

public interface IPriceCardRepository
{
    Task<List<PriceCard>> GetPriceCardsAsync(Guid tenantId, PriceCardType? type = null, string? search = null, bool? isActive = null);
    Task<PriceCard?> GetPriceCardByIdAsync(Guid tenantId, Guid priceCardId, bool includeLines = true);
    Task<PriceCard?> GetPriceCardByCodeAsync(Guid tenantId, string code);
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null);
    Task AddPriceCardAsync(PriceCard priceCard);

    Task<List<PriceCardLine>> GetLinesByCardIdAsync(Guid tenantId, Guid priceCardId);
    Task<PriceCardLine?> GetLineByIdAsync(Guid tenantId, Guid priceCardId, Guid lineId);
    Task AddLineAsync(PriceCardLine line);
    Task RemoveLineAsync(PriceCardLine line);
    Task RemoveLinesAsync(Guid tenantId, Guid priceCardId);

    Task<PriceCardLine?> GetActiveLineAsync(
        Guid tenantId,
        Guid priceCardId,
        Guid productId,
        Guid? uomId,
        DateTime asOfDate);

    Task<bool> ProductExistsAsync(Guid tenantId, Guid productId);
    Task<bool> UnitOfMeasureExistsAsync(Guid tenantId, Guid unitOfMeasureId);
    Task<bool> CurrencyExistsAsync(Guid tenantId, string currencyCode);
    Task<bool> LineExistsAsync(Guid tenantId, Guid priceCardId, Guid productId, Guid? uomId, Guid? excludeLineId = null);

    Task SaveChangesAsync();
}
