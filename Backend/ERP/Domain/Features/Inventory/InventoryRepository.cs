using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory;

public class InventoryRepository : IInventoryRepository
{
    private readonly DomainDbContext _context;

    public InventoryRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryTransaction?> GetTransactionByIdAsync(Guid tenantId, Guid transactionId)
    {
        return await _context.Set<InventoryTransaction>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.InventoryTransactionId == transactionId);
    }

    public async Task<List<InventoryTransaction>> GetTransactionsByOriginalIdAsync(Guid tenantId, Guid originalTransactionId)
    {
        return await _context.Set<InventoryTransaction>()
            .Where(x => x.TenantId == tenantId && x.ReversalOfTransactionId == originalTransactionId)
            .ToListAsync();
    }

    public async Task<bool> TransactionExistsByIdempotencyKeyAsync(
        Guid tenantId,
        SourceDocumentType documentType,
        Guid documentId,
        Guid documentLineId,
        InventoryMovementType movementType,
        int sequence)
    {
        return await _context.Set<InventoryTransaction>()
            .AnyAsync(x => x.TenantId == tenantId
                           && x.SourceDocumentType == documentType
                           && x.SourceDocumentId == documentId
                           && x.SourceDocumentLineId == documentLineId
                           && x.MovementType == movementType
                           && x.MovementSequence == sequence);
    }

    public async Task AddTransactionAsync(InventoryTransaction transaction)
    {
        await _context.Set<InventoryTransaction>().AddAsync(transaction);
    }

    public async Task<InventoryBalance?> GetBalanceAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        string batchNumber,
        string serialNumber)
    {
        string normBatch = batchNumber?.Trim() ?? string.Empty;
        string normSerial = serialNumber?.Trim() ?? string.Empty;

        return await _context.Set<InventoryBalance>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId
                                      && x.WarehouseLocationId == locationId
                                      && x.ProductId == productId
                                      && x.BatchNumber == normBatch
                                      && x.SerialNumber == normSerial);
    }

    public async Task<List<InventoryBalance>> GetBalancesByProductAsync(Guid tenantId, Guid productId)
    {
        return await _context.Set<InventoryBalance>()
            .Where(x => x.TenantId == tenantId && x.ProductId == productId)
            .ToListAsync();
    }

    public async Task<List<InventoryBalance>> GetBalancesByLocationAsync(Guid tenantId, Guid locationId)
    {
        return await _context.Set<InventoryBalance>()
            .Where(x => x.TenantId == tenantId && x.WarehouseLocationId == locationId)
            .ToListAsync();
    }

    public async Task AddBalanceAsync(InventoryBalance balance)
    {
        await _context.Set<InventoryBalance>().AddAsync(balance);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
