using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public class WeightedAverageValuationStrategy : IInventoryValuationStrategy
{
    private readonly IInventoryRepository _inventoryRepository;

    public WeightedAverageValuationStrategy(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public ValuationMethod Method => ValuationMethod.WeightedAverage;

    private async Task<ProductValuationBalance> GetOrCreateBalanceAsync(ValuationContext context)
    {
        var balance = await _inventoryRepository.GetValuationBalanceAsync(
            context.TenantId,
            context.ProductId,
            context.WarehouseId);

        if (balance == null)
        {
            balance = ProductValuationBalance.Create(
                context.TenantId,
                context.ProductId,
                context.WarehouseId);
            await _inventoryRepository.AddValuationBalanceAsync(balance);
        }

        return balance;
    }

    public async Task<ValuationInflowResult> ProcessInflowAsync(
        ValuationContext context,
        decimal quantity,
        decimal unitCost,
        Guid transactionId)
    {
        var balance = await GetOrCreateBalanceAsync(context);
        decimal inflowValue = balance.ApplyInflow(quantity, unitCost);

        return new ValuationInflowResult
        {
            UnitCost = unitCost,
            TotalCost = inflowValue,
            VarianceAmount = 0
        };
    }

    public async Task<ValuationOutflowResult> ProcessOutflowAsync(
        ValuationContext context,
        decimal quantity,
        Guid transactionId)
    {
        var balance = await GetOrCreateBalanceAsync(context);
        decimal issueCost = balance.ApplyOutflow(quantity);

        return new ValuationOutflowResult
        {
            UnitCost = balance.CurrentAverageCost,
            TotalCost = issueCost
        };
    }

    public async Task<ValuationReversalResult> ProcessInflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalInflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        var balance = await GetOrCreateBalanceAsync(context);
        decimal deductionCost = balance.ApplyInflowReversal(reversalQuantity, originalInflowTxn.UnitCost);

        return new ValuationReversalResult
        {
            ReversalUnitCost = balance.CurrentAverageCost,
            ReversalTotalCost = deductionCost,
            VarianceAmount = 0
        };
    }

    public async Task<ValuationReversalResult> ProcessOutflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalOutflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        var balance = await GetOrCreateBalanceAsync(context);
        decimal restoredValue = balance.ApplyOutflowReversal(reversalQuantity, originalOutflowTxn.UnitCost);

        return new ValuationReversalResult
        {
            ReversalUnitCost = originalOutflowTxn.UnitCost,
            ReversalTotalCost = restoredValue,
            VarianceAmount = 0
        };
    }
}
