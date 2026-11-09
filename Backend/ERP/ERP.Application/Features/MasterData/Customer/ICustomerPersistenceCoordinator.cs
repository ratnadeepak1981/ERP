using System;
using System.Threading.Tasks;
using CurrencyEntity = Domain.Features.MasterData.Currency.Currency;

namespace ERP.Application.Features.MasterData.Customer;

public interface ICustomerPersistenceCoordinator
{
    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();
    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);
    Task SaveChangesAsync();
    Task<CurrencyEntity?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId);
}
