using System;
using System.Threading.Tasks;

namespace ERP.Domain.Features.Inventory.Valuation;

public interface IInventoryValuationService
{
    Task AcquireValuationLockAsync(Guid tenantId, Guid productId, Guid warehouseId);

    Task AcquireTransferValuationLocksAsync(Guid tenantId, Guid productId, Guid sourceWarehouseId, Guid destinationWarehouseId);

    Task<ValuationInflowResult> ProcessInflowAsync(
        ValuationContext context,
        decimal quantity,
        decimal unitCost,
        Guid transactionId);

    Task<ValuationOutflowResult> ProcessOutflowAsync(
        ValuationContext context,
        decimal quantity,
        Guid transactionId);

    Task<ValuationReversalResult> ProcessInflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalInflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId);

    Task<ValuationReversalResult> ProcessOutflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalOutflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId);
}
