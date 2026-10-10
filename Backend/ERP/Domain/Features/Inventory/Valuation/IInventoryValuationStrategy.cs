using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public interface IInventoryValuationStrategy
{
    ValuationMethod Method { get; }

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
