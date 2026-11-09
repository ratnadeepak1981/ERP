using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.Inventory.Putaway;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Application.Features.MasterData.Warehouse;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WarehouseEntity = ERP.Domain.Features.MasterData.Warehouse.Warehouse;

namespace ERP.Infrastructure.Persistence;

public class WarehousePersistenceCoordinator : IWarehousePersistenceCoordinator
{
    private readonly DomainDbContext _context;

    public WarehousePersistenceCoordinator(DomainDbContext context)
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

    public Task<WarehouseEntity?> GetWarehouseRawByIdAsync(Guid tenantId, Guid warehouseId) =>
        _context.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId);

    public Task<WarehouseZone?> GetZoneRawByIdAsync(Guid tenantId, Guid zoneId) =>
        _context.WarehouseZones.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseZoneId == zoneId);

    public Task<WarehouseLocation?> GetLocationRawByIdAsync(Guid tenantId, Guid locationId) =>
        _context.WarehouseLocations.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseLocationId == locationId);

    public async Task ValidateWarehouseDeactivationSafetyAsync(Guid tenantId, WarehouseEntity warehouse)
    {
        var warehouseBalances = await _context.InventoryBalances
            .Where(b => b.TenantId == tenantId && b.WarehouseId == warehouse.WarehouseId && (b.QuantityOnHand > 0 || b.QuantityAllocated > 0))
            .ToListAsync();

        decimal totalOnHand = warehouseBalances.Sum(b => b.QuantityOnHand);
        decimal totalAllocated = warehouseBalances.Sum(b => b.QuantityAllocated);

        if (totalOnHand > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate warehouse '{warehouse.WarehouseCode}' because on-hand stock ({totalOnHand}) remains in the warehouse.");
        }

        if (totalAllocated > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate warehouse '{warehouse.WarehouseCode}' because allocated stock ({totalAllocated}) remains in the warehouse.");
        }

        var draftGrnNumber = await _context.GoodsReceiptNoteLines
            .Where(l => l.WarehouseId == warehouse.WarehouseId)
            .Join(_context.GoodsReceiptNotes.Where(g => g.TenantId == tenantId && g.Status == GoodsReceiptNoteStatus.Draft),
                  l => l.GoodsReceiptNoteId,
                  g => g.GoodsReceiptNoteId,
                  (l, g) => g.GrnNumber)
            .FirstOrDefaultAsync();

        if (draftGrnNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate warehouse '{warehouse.WarehouseCode}' because pending draft Goods Receipt Note '{draftGrnNumber}' references this warehouse.");
        }

        var draftPutawayNumber = await _context.Putaways
            .Where(p => p.TenantId == tenantId && p.WarehouseId == warehouse.WarehouseId && p.Status == PutawayStatus.Draft)
            .Select(p => p.PutawayNumber)
            .FirstOrDefaultAsync();

        if (draftPutawayNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate warehouse '{warehouse.WarehouseCode}' because pending draft Putaway '{draftPutawayNumber}' exists for this warehouse.");
        }

        var draftShipmentNumber = await _context.SalesShipments
            .Where(s => s.TenantId == tenantId && s.WarehouseId == warehouse.WarehouseId && s.Status == SalesShipmentStatus.Draft)
            .Select(s => s.ShipmentNumber)
            .FirstOrDefaultAsync();

        if (draftShipmentNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate warehouse '{warehouse.WarehouseCode}' because pending draft Sales Shipment '{draftShipmentNumber}' exists for this warehouse.");
        }
    }

    public async Task ValidateZoneDeactivationSafetyAsync(Guid tenantId, WarehouseZone zone)
    {
        var zoneLocationIds = await _context.WarehouseLocations
            .Where(l => l.TenantId == tenantId && l.WarehouseZoneId == zone.WarehouseZoneId)
            .Select(l => l.WarehouseLocationId)
            .ToListAsync();

        if (zoneLocationIds.Count == 0) return;

        var balances = await _context.InventoryBalances
            .Where(b => b.TenantId == tenantId && zoneLocationIds.Contains(b.WarehouseLocationId) && (b.QuantityOnHand > 0 || b.QuantityAllocated > 0))
            .ToListAsync();

        decimal totalOnHand = balances.Sum(b => b.QuantityOnHand);
        decimal totalAllocated = balances.Sum(b => b.QuantityAllocated);

        if (totalOnHand > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because on-hand stock ({totalOnHand}) remains in locations assigned to this zone.");
        }

        if (totalAllocated > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because allocated stock ({totalAllocated}) remains in locations assigned to this zone.");
        }

        var draftGrnNumber = await _context.GoodsReceiptNoteLines
            .Where(l => zoneLocationIds.Contains(l.WarehouseLocationId))
            .Join(_context.GoodsReceiptNotes.Where(g => g.TenantId == tenantId && g.Status == GoodsReceiptNoteStatus.Draft),
                  l => l.GoodsReceiptNoteId,
                  g => g.GoodsReceiptNoteId,
                  (l, g) => g.GrnNumber)
            .FirstOrDefaultAsync();

        if (draftGrnNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because pending draft Goods Receipt Note '{draftGrnNumber}' references location(s) in this zone.");
        }

        var draftPutawaySource = await _context.PutawayLines
            .Where(l => zoneLocationIds.Contains(l.SourceWarehouseLocationId))
            .Join(_context.Putaways.Where(p => p.TenantId == tenantId && p.Status == PutawayStatus.Draft),
                  l => l.PutawayId,
                  p => p.PutawayId,
                  (l, p) => p.PutawayNumber)
            .FirstOrDefaultAsync();

        if (draftPutawaySource != null)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because pending draft Putaway '{draftPutawaySource}' references location(s) in this zone as source.");
        }

        var draftPutawayDest = await _context.PutawayLineAllocations
            .Where(a => zoneLocationIds.Contains(a.DestinationWarehouseLocationId))
            .Join(_context.PutawayLines,
                  a => a.PutawayLineId,
                  l => l.PutawayLineId,
                  (a, l) => l.PutawayId)
            .Join(_context.Putaways.Where(p => p.TenantId == tenantId && p.Status == PutawayStatus.Draft),
                  putawayId => putawayId,
                  p => p.PutawayId,
                  (putawayId, p) => p.PutawayNumber)
            .FirstOrDefaultAsync();

        if (draftPutawayDest != null)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because pending draft Putaway '{draftPutawayDest}' references location(s) in this zone as destination.");
        }

        var draftShipmentNumber = await _context.SalesShipmentItems
            .Where(i => i.IsActive && zoneLocationIds.Contains(i.WarehouseLocationId))
            .Join(_context.SalesShipments.Where(s => s.TenantId == tenantId && s.Status == SalesShipmentStatus.Draft),
                  i => i.SalesShipmentId,
                  s => s.Id,
                  (i, s) => s.ShipmentNumber)
            .FirstOrDefaultAsync();

        if (draftShipmentNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate zone '{zone.ZoneCode}' because pending draft Sales Shipment '{draftShipmentNumber}' references location(s) in this zone.");
        }
    }

    public async Task ValidateLocationDeactivationSafetyAsync(Guid tenantId, WarehouseLocation location, List<Guid> subtreeLocationIds)
    {
        // 1. Check stock balances across subtree locations
        var balances = await _context.InventoryBalances
            .Where(b => b.TenantId == tenantId && subtreeLocationIds.Contains(b.WarehouseLocationId) && (b.QuantityOnHand > 0 || b.QuantityAllocated > 0))
            .ToListAsync();

        decimal totalOnHand = balances.Sum(b => b.QuantityOnHand);
        decimal totalAllocated = balances.Sum(b => b.QuantityAllocated);

        if (totalOnHand > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because on-hand stock ({totalOnHand}) remains in the location scope.");
        }

        if (totalAllocated > 0)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because allocated stock ({totalAllocated}) remains in the location scope.");
        }

        // 2. Check pending draft Goods Receipt Notes
        var draftGrnNumber = await _context.GoodsReceiptNoteLines
            .Where(l => subtreeLocationIds.Contains(l.WarehouseLocationId))
            .Join(_context.GoodsReceiptNotes.Where(g => g.TenantId == tenantId && g.Status == GoodsReceiptNoteStatus.Draft),
                  l => l.GoodsReceiptNoteId,
                  g => g.GoodsReceiptNoteId,
                  (l, g) => g.GrnNumber)
            .FirstOrDefaultAsync();

        if (draftGrnNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because pending draft Goods Receipt Note '{draftGrnNumber}' references it.");
        }

        // 3. Check pending draft Putaway orders
        var draftPutawaySource = await _context.PutawayLines
            .Where(l => subtreeLocationIds.Contains(l.SourceWarehouseLocationId))
            .Join(_context.Putaways.Where(p => p.TenantId == tenantId && p.Status == PutawayStatus.Draft),
                  l => l.PutawayId,
                  p => p.PutawayId,
                  (l, p) => p.PutawayNumber)
            .FirstOrDefaultAsync();

        if (draftPutawaySource != null)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because pending draft Putaway '{draftPutawaySource}' references it as source.");
        }

        var draftPutawayDest = await _context.PutawayLineAllocations
            .Where(a => subtreeLocationIds.Contains(a.DestinationWarehouseLocationId))
            .Join(_context.PutawayLines,
                  a => a.PutawayLineId,
                  l => l.PutawayLineId,
                  (a, l) => l.PutawayId)
            .Join(_context.Putaways.Where(p => p.TenantId == tenantId && p.Status == PutawayStatus.Draft),
                  putawayId => putawayId,
                  p => p.PutawayId,
                  (putawayId, p) => p.PutawayNumber)
            .FirstOrDefaultAsync();

        if (draftPutawayDest != null)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because pending draft Putaway '{draftPutawayDest}' references it as destination.");
        }

        // 4. Check pending draft Sales Shipments
        var draftShipmentNumber = await _context.SalesShipmentItems
            .Where(i => i.IsActive && subtreeLocationIds.Contains(i.WarehouseLocationId))
            .Join(_context.SalesShipments.Where(s => s.TenantId == tenantId && s.Status == SalesShipmentStatus.Draft),
                  i => i.SalesShipmentId,
                  s => s.Id,
                  (i, s) => s.ShipmentNumber)
            .FirstOrDefaultAsync();

        if (draftShipmentNumber != null)
        {
            throw new InvalidOperationException($"Cannot deactivate location '{location.LocationCode}' because pending draft Sales Shipment '{draftShipmentNumber}' references it.");
        }
    }
}
