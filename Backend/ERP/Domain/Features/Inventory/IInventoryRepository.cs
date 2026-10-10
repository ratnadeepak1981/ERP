using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory;

public interface IInventoryRepository
{
    Task<InventoryTransaction?> GetTransactionByIdAsync(Guid tenantId, Guid transactionId);
    Task<List<InventoryTransaction>> GetTransactionsByOriginalIdAsync(Guid tenantId, Guid originalTransactionId);
    Task<bool> TransactionExistsByIdempotencyKeyAsync(
        Guid tenantId,
        SourceDocumentType documentType,
        Guid documentId,
        Guid documentLineId,
        InventoryMovementType movementType,
        int sequence);

    Task AddTransactionAsync(InventoryTransaction transaction);

    Task<InventoryBalance?> GetBalanceAsync(
        Guid tenantId,
        Guid locationId,
        Guid productId,
        string batchNumber,
        string serialNumber);

    Task<List<InventoryBalance>> GetBalancesByProductAsync(Guid tenantId, Guid productId);
    Task<List<InventoryBalance>> GetBalancesByLocationAsync(Guid tenantId, Guid locationId);

    Task AddBalanceAsync(InventoryBalance balance);

    Task SaveChangesAsync();
}
