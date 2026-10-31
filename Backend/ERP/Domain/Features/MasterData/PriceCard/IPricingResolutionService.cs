using System;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.PriceCard;

public interface IPricingResolutionService
{
    Task<PriceResolutionResult> ResolvePurchasePriceAsync(
        Guid tenantId,
        Guid? supplierId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? transactionDate = null,
        Guid? overridePriceCardId = null);

    Task<PriceResolutionResult> ResolveSalesPriceAsync(
        Guid tenantId,
        Guid customerId,
        Guid productId,
        Guid? requestedUomId,
        DateTime? transactionDate = null,
        Guid? overridePriceCardId = null);
}
