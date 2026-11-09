using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.Procurement;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.Infrastructure.Persistence;

public class ProcurementPersistenceCoordinator : IProcurementPersistenceCoordinator
{
    private readonly DomainDbContext _context;
    private IDbContextTransaction? _currentTx;

    public ProcurementPersistenceCoordinator(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task EnsureInventoryBalanceAsync(Guid tenantId, Guid warehouseId, Guid locationId, Guid productId)
    {
        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId
                                   && b.WarehouseId == warehouseId
                                   && b.WarehouseLocationId == locationId
                                   && b.ProductId == productId);

        if (balance == null)
        {
            balance = InventoryBalance.Create(
                tenantId: tenantId,
                warehouseId: warehouseId,
                warehouseLocationId: locationId,
                productId: productId,
                batchNumber: null,
                serialNumber: null);

            _context.InventoryBalances.Add(balance);
        }
    }

    public async Task ApplyOnOrderDeltaAsync(
        Guid tenantId,
        Guid warehouseId,
        Guid locationId,
        Guid productId,
        decimal baseDelta,
        bool isApproval)
    {
        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId
                                   && b.WarehouseId == warehouseId
                                   && b.WarehouseLocationId == locationId
                                   && b.ProductId == productId
                                   && b.BatchNumber == string.Empty
                                   && b.SerialNumber == string.Empty);

        if (balance == null && isApproval)
        {
            balance = InventoryBalance.Create(
                tenantId,
                warehouseId,
                locationId,
                productId,
                batchNumber: null,
                serialNumber: null);
            _context.InventoryBalances.Add(balance);
        }

        if (balance != null)
        {
            decimal appliedDelta = isApproval ? baseDelta : -Math.Min(balance.QuantityOnOrder, baseDelta);
            if (appliedDelta != 0)
            {
                balance.ApplyOnOrderChange(appliedDelta);
            }
        }
    }

    public async Task DecrementOnOrderBalanceAsync(Guid tenantId, Guid warehouseId, Guid productId, decimal baseQuantity)
    {
        var onOrderBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId
                                       && b.WarehouseId == warehouseId
                                       && b.ProductId == productId
                                       && b.QuantityOnOrder > 0);

        if (onOrderBalance != null)
        {
            decimal decrement = Math.Min(onOrderBalance.QuantityOnOrder, baseQuantity);
            if (decrement > 0)
            {
                onOrderBalance.ApplyOnOrderChange(-decrement);
            }
        }
    }

    public Task RecordAuditAsync(DomainAuditRecord record)
    {
        _context.DomainAuditRecords.Add(record);
        return Task.CompletedTask;
    }

    public async Task<Company?> GetActiveCompanyWithBaseCurrencyAsync(Guid tenantId, Guid companyId)
    {
        return await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive);
    }

    public async Task<List<Warehouse>> GetActiveBranchWarehousesAsync(Guid tenantId, Guid branchId)
    {
        return await _context.Warehouses
            .Where(w => w.TenantId == tenantId && w.BranchId == branchId && w.IsActive)
            .ToListAsync();
    }

    public async Task<Warehouse?> GetActiveCompanyWarehouseAsync(Guid tenantId, Guid companyId)
    {
        return await _context.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.CompanyId == companyId && w.IsActive);
    }

    public async Task<List<WarehouseLocation>> GetActiveLocationsAsync(Guid tenantId, Guid warehouseId)
    {
        return await _context.WarehouseLocations
            .Where(l => l.TenantId == tenantId && l.WarehouseId == warehouseId && l.IsActive)
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, Product>> GetProductsByIdsAsync(Guid tenantId, IEnumerable<Guid> productIds)
    {
        var idList = productIds.Distinct().ToList();
        return await _context.Products
            .Where(p => p.TenantId == tenantId && idList.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);
    }

    public async Task<List<GoodsReceiptNoteLine>> GetPreviouslyConfirmedPoLinesAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseOrderId,
        Guid currentGrnId,
        IEnumerable<Guid> poItemIds)
    {
        var idList = poItemIds.Distinct().ToList();
        return await _context.GoodsReceiptNotes
            .Where(existing => existing.TenantId == tenantId &&
                               existing.CompanyId == companyId &&
                               existing.PurchaseOrderId == purchaseOrderId &&
                               existing.GoodsReceiptNoteId != currentGrnId &&
                               existing.Status == GoodsReceiptNoteStatus.Confirmed)
            .SelectMany(existing => existing.Lines)
            .Where(line => idList.Contains(line.PurchaseOrderItemId))
            .ToListAsync();
    }

    public async Task<decimal> GetPreviouslyAllocatedAmountAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseOrderId,
        Guid poItemId)
    {
        return await _context.GoodsReceiptNotes
            .Where(grn => grn.TenantId == tenantId &&
                          grn.CompanyId == companyId &&
                          grn.PurchaseOrderId == purchaseOrderId &&
                          grn.Status == GoodsReceiptNoteStatus.Confirmed)
            .SelectMany(grn => grn.Lines)
            .Where(line => line.PurchaseOrderItemId == poItemId)
            .SumAsync(line => (decimal?)line.LineTotal) ?? 0m;
    }

    public Task ReplaceGrnLinesAsync(GoodsReceiptNote grn, IEnumerable<GoodsReceiptNoteLine> newLines)
    {
        var existingLines = grn.Lines.ToList();
        grn.Lines.Clear();
        _context.GoodsReceiptNoteLines.RemoveRange(existingLines);
        var replacementList = newLines.ToList();
        grn.Lines.AddRange(replacementList);
        _context.GoodsReceiptNoteLines.AddRange(replacementList);
        grn.RecalculateTotal();
        return Task.CompletedTask;
    }

    public async Task<IAsyncDisposable> BeginTransactionAsync()
    {
        _currentTx = await _context.Database.BeginTransactionAsync();
        return _currentTx;
    }

    public async Task CommitTransactionAsync()
    {
        if (_currentTx != null)
        {
            await _currentTx.CommitAsync();
            await _currentTx.DisposeAsync();
            _currentTx = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_currentTx != null)
        {
            await _currentTx.RollbackAsync();
            await _currentTx.DisposeAsync();
            _currentTx = null;
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
