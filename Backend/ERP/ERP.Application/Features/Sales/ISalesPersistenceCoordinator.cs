using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.Shipment;

namespace ERP.Application.Features.Sales;

public interface ISalesPersistenceCoordinator
{
    bool HasActiveTransaction { get; }

    Task<IAsyncDisposable?> BeginTransactionIfNeededAsync();

    Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx);

    Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx);

    Task SaveChangesAsync();

    Task ExecuteStrategyAsync(Func<Task> operation);

    Task<T> ExecuteStrategyAsync<T>(Func<Task<T>> operation);

    Task AcquireSalesOrderUpdateLockAsync(Guid tenantId, Guid salesOrderId);

    Task<Currency> GetActiveCompanyBaseCurrencyAsync(Guid tenantId, Guid companyId);

    void RemoveShipmentItems(IEnumerable<SalesShipmentItem> items);

    void RemoveInvoiceItems(IEnumerable<SalesInvoiceItem> items);
}
