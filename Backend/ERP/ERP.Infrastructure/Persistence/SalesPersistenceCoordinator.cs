using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Infrastructure.Persistence;

public class SalesPersistenceCoordinator : ISalesPersistenceCoordinator
{
    private readonly DomainDbContext _context;

    public SalesPersistenceCoordinator(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public bool HasActiveTransaction => _context.Database.CurrentTransaction != null;

    public async Task<IAsyncDisposable?> BeginTransactionIfNeededAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null; // participating in outer ambient transaction
        }

        return await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionIfNeededAsync(IAsyncDisposable? tx)
    {
        if (tx is IDbContextTransaction dbTx)
        {
            await dbTx.CommitAsync();
            await dbTx.DisposeAsync();
        }
    }

    public async Task RollbackTransactionIfNeededAsync(IAsyncDisposable? tx)
    {
        if (tx is IDbContextTransaction dbTx)
        {
            try
            {
                await dbTx.RollbackAsync();
            }
            finally
            {
                await dbTx.DisposeAsync();
            }
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task ExecuteStrategyAsync(Func<Task> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(operation);
    }

    public async Task<T> ExecuteStrategyAsync<T>(Func<Task<T>> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(operation);
    }

    public async Task AcquireSalesOrderUpdateLockAsync(Guid tenantId, Guid salesOrderId)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM SalesOrders WITH (UPDLOCK, ROWLOCK) WHERE Id = {salesOrderId} AND TenantId = {tenantId}");
    }

    public async Task<Currency> GetActiveCompanyBaseCurrencyAsync(Guid tenantId, Guid companyId)
    {
        var company = await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.Id == companyId &&
                c.IsActive);

        if (company?.BaseCurrency == null ||
            company.BaseCurrency.TenantId != tenantId ||
            !company.BaseCurrency.IsActive)
        {
            throw new InvalidOperationException("The selected company has no active base currency.");
        }

        return company.BaseCurrency;
    }

    public void RemoveShipmentItems(IEnumerable<SalesShipmentItem> items)
    {
        _context.SalesShipmentItems.RemoveRange(items);
    }

    public void RemoveInvoiceItems(IEnumerable<SalesInvoiceItem> items)
    {
        _context.SalesInvoiceItems.RemoveRange(items);
    }
}
