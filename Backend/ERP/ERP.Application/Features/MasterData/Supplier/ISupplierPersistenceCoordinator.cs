using System;
using System.Threading.Tasks;
using CurrencyEntity = Domain.Features.MasterData.Currency.Currency;
using PriceCardEntity = Domain.Features.MasterData.PriceCard.PriceCard;

namespace ERP.Application.Features.MasterData.Supplier;

public interface ISupplierPersistenceCoordinator
{
    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();
    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task SaveChangesAsync();
    Task<CurrencyEntity?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId);
    Task<PriceCardEntity?> GetPriceCardByIdAsync(Guid tenantId, Guid priceCardId);
}
