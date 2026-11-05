using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.PriceCard;
using CurrencyEntity = Domain.Features.MasterData.Currency.Currency;

namespace ERP.Application.Features.MasterData.Supplier;

public interface ISupplierPersistenceCoordinator
{
    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();
    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task SaveChangesAsync();
    Task<CurrencyEntity?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId);
    Task<PriceCard?> GetPriceCardByIdAsync(Guid tenantId, Guid priceCardId);
}
