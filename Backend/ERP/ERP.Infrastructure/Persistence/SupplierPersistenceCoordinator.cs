using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.PriceCard;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.MasterData.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CurrencyEntity = Domain.Features.MasterData.Currency.Currency;

namespace ERP.Infrastructure.Persistence;

public class SupplierPersistenceCoordinator : ISupplierPersistenceCoordinator
{
    private readonly DomainDbContext _context;

    public SupplierPersistenceCoordinator(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IAsyncDisposable?> BeginTransactionIfNeededAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
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
            await dbTx.RollbackAsync();
            await dbTx.DisposeAsync();
        }
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public Task<CurrencyEntity?> GetCurrencyByIdAsync(Guid tenantId, Guid currencyId) =>
        _context.Currencies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyId == currencyId);

    public Task<PriceCard?> GetPriceCardByIdAsync(Guid tenantId, Guid priceCardId) =>
        _context.PriceCards.FirstOrDefaultAsync(pc => pc.TenantId == tenantId && pc.PriceCardId == priceCardId);
}
