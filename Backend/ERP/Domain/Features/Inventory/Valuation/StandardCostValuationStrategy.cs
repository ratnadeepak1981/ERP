using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public class StandardCostValuationStrategy : IInventoryValuationStrategy
{
    public ValuationMethod Method => ValuationMethod.StandardCost;

    private void EnsureValidStandardCost(ValuationContext context)
    {
        if (context.Product.StandardCost <= 0)
        {
            throw new InvalidOperationException(
                $"Product '{context.Product.ProductCode}' uses Standard Cost valuation, " +
                "but no valid StandardCost (> 0) is configured. Actual cost cannot be silently substituted.");
        }
    }

    public Task<ValuationInflowResult> ProcessInflowAsync(
        ValuationContext context,
        decimal quantity,
        decimal unitCost,
        Guid transactionId)
    {
        EnsureValidStandardCost(context);

        if (quantity <= 0)
            throw new ArgumentException("Inflow quantity must be greater than zero.", nameof(quantity));

        decimal standardUnitCost = context.Product.StandardCost;
        decimal standardTotal = Math.Round(quantity * standardUnitCost, 2, MidpointRounding.AwayFromZero);
        decimal purchasePriceVariance = Math.Round(quantity * (unitCost - standardUnitCost), 2, MidpointRounding.AwayFromZero);

        return Task.FromResult(new ValuationInflowResult
        {
            UnitCost = standardUnitCost,
            TotalCost = standardTotal,
            VarianceAmount = purchasePriceVariance
        });
    }

    public Task<ValuationOutflowResult> ProcessOutflowAsync(
        ValuationContext context,
        decimal quantity,
        Guid transactionId)
    {
        EnsureValidStandardCost(context);

        if (quantity <= 0)
            throw new ArgumentException("Outflow quantity must be greater than zero.", nameof(quantity));

        decimal standardUnitCost = context.Product.StandardCost;
        decimal issueTotal = Math.Round(quantity * standardUnitCost, 2, MidpointRounding.AwayFromZero);

        return Task.FromResult(new ValuationOutflowResult
        {
            UnitCost = standardUnitCost,
            TotalCost = issueTotal
        });
    }

    public Task<ValuationReversalResult> ProcessInflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalInflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        EnsureValidStandardCost(context);

        decimal standardUnitCost = context.Product.StandardCost;
        decimal reversalTotal = Math.Round(reversalQuantity * standardUnitCost, 2, MidpointRounding.AwayFromZero);
        decimal reversalPpv = Math.Round(reversalQuantity * (originalInflowTxn.UnitCost - standardUnitCost), 2, MidpointRounding.AwayFromZero);

        return Task.FromResult(new ValuationReversalResult
        {
            ReversalUnitCost = standardUnitCost,
            ReversalTotalCost = reversalTotal,
            VarianceAmount = reversalPpv
        });
    }

    public Task<ValuationReversalResult> ProcessOutflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalOutflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        EnsureValidStandardCost(context);

        decimal standardUnitCost = context.Product.StandardCost;
        decimal restoredTotal = Math.Round(reversalQuantity * standardUnitCost, 2, MidpointRounding.AwayFromZero);

        return Task.FromResult(new ValuationReversalResult
        {
            ReversalUnitCost = standardUnitCost,
            ReversalTotalCost = restoredTotal,
            VarianceAmount = 0
        });
    }
}
