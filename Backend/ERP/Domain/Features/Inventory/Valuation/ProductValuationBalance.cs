using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.Inventory.Valuation;

public class ProductValuationBalance : Auditable, ITenantScopedEntity
{
    public Guid ProductValuationBalanceId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public decimal TotalQuantity { get; private set; }

    public decimal TotalCostValue { get; private set; }

    public decimal CurrentAverageCost { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ProductValuationBalance Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid? productValuationBalanceId = null)
    {
        return new ProductValuationBalance
        {
            ProductValuationBalanceId = productValuationBalanceId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            TotalQuantity = 0,
            TotalCostValue = 0,
            CurrentAverageCost = 0
        };
    }

    public decimal ApplyInflow(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
            throw new ArgumentException("Inflow quantity must be greater than zero.", nameof(quantity));

        if (unitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        decimal inflowValue = Math.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);
        decimal newQty = TotalQuantity + quantity;
        decimal newValue = TotalCostValue + inflowValue;

        CurrentAverageCost = newQty > 0
            ? Math.Round(newValue / newQty, 4, MidpointRounding.AwayFromZero)
            : 0;

        TotalQuantity = newQty;
        TotalCostValue = newValue;

        return inflowValue;
    }

    public decimal ApplyOutflow(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Outflow quantity must be greater than zero.", nameof(quantity));

        if (quantity > TotalQuantity)
            throw new InvalidOperationException(
                $"Insufficient valuation inventory. Requested: {quantity}, Available: {TotalQuantity}.");

        decimal issueCost;

        if (quantity == TotalQuantity)
        {
            // Fully exhausted: absorb any accumulated fractional cents completely
            issueCost = TotalCostValue;
            TotalQuantity = 0;
            TotalCostValue = 0;
            // Retain CurrentAverageCost as the last established price
        }
        else
        {
            issueCost = Math.Round(quantity * CurrentAverageCost, 2, MidpointRounding.AwayFromZero);
            TotalQuantity -= quantity;
            TotalCostValue -= issueCost;

            if (TotalCostValue < 0)
            {
                TotalCostValue = 0;
            }

            if (TotalQuantity > 0)
            {
                CurrentAverageCost = Math.Round(TotalCostValue / TotalQuantity, 4, MidpointRounding.AwayFromZero);
            }
        }

        return issueCost;
    }

    public decimal ApplyInflowReversal(decimal quantity, decimal originalReceiptCost)
    {
        if (quantity <= 0)
            throw new ArgumentException("Reversal quantity must be greater than zero.", nameof(quantity));

        if (quantity > TotalQuantity)
            throw new InvalidOperationException(
                $"Cannot reverse {quantity} units: total available valuation quantity is only {TotalQuantity}.");

        decimal deductionCost;

        if (quantity == TotalQuantity)
        {
            // Exact full unwind
            deductionCost = TotalCostValue;
            TotalQuantity = 0;
            TotalCostValue = 0;
        }
        else
        {
            // Relieve at current moving average cost
            deductionCost = Math.Round(quantity * CurrentAverageCost, 2, MidpointRounding.AwayFromZero);
            TotalQuantity -= quantity;
            TotalCostValue -= deductionCost;

            if (TotalCostValue < 0)
            {
                TotalCostValue = 0;
            }

            if (TotalQuantity > 0)
            {
                CurrentAverageCost = Math.Round(TotalCostValue / TotalQuantity, 4, MidpointRounding.AwayFromZero);
            }
        }

        return deductionCost;
    }

    public decimal ApplyOutflowReversal(decimal quantity, decimal historicalUnitCost)
    {
        if (quantity <= 0)
            throw new ArgumentException("Reversal restoration quantity must be greater than zero.", nameof(quantity));

        if (historicalUnitCost < 0)
            throw new ArgumentException("Historical unit cost cannot be negative.", nameof(historicalUnitCost));

        decimal restoredValue = Math.Round(quantity * historicalUnitCost, 2, MidpointRounding.AwayFromZero);
        decimal newQty = TotalQuantity + quantity;
        decimal newValue = TotalCostValue + restoredValue;

        CurrentAverageCost = newQty > 0
            ? Math.Round(newValue / newQty, 4, MidpointRounding.AwayFromZero)
            : 0;

        TotalQuantity = newQty;
        TotalCostValue = newValue;

        return restoredValue;
    }

    private ProductValuationBalance()
    {
    }
}
