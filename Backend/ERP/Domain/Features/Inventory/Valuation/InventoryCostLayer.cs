using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.Inventory.Valuation;

public class InventoryCostLayer : Auditable, ITenantScopedEntity
{
    public Guid CostLayerId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid WarehouseLocationId { get; private set; }

    public Guid InventoryTransactionId { get; private set; }

    public DateTime LayerDate { get; private set; }

    public decimal OriginalQuantity { get; private set; }

    public decimal RemainingQuantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal OriginalCost { get; private set; }

    public decimal RemainingCost { get; private set; }

    public string BatchNumber { get; private set; } = string.Empty;

    public string SerialNumber { get; private set; } = string.Empty;

    public bool IsExhausted { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static InventoryCostLayer Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid warehouseLocationId,
        Guid inventoryTransactionId,
        DateTime layerDate,
        decimal quantity,
        decimal unitCost,
        string? batchNumber = null,
        string? serialNumber = null,
        Guid? costLayerId = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Initial layer quantity must be greater than zero.", nameof(quantity));

        if (unitCost < 0)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(unitCost));

        decimal totalCost = Math.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);

        return new InventoryCostLayer
        {
            CostLayerId = costLayerId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            InventoryTransactionId = inventoryTransactionId,
            LayerDate = layerDate,
            OriginalQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost,
            OriginalCost = totalCost,
            RemainingCost = totalCost,
            BatchNumber = batchNumber?.Trim() ?? string.Empty,
            SerialNumber = serialNumber?.Trim() ?? string.Empty,
            IsExhausted = false
        };
    }

    public decimal Consume(decimal quantityToConsume)
    {
        if (quantityToConsume <= 0)
            throw new ArgumentException("Consumption quantity must be greater than zero.", nameof(quantityToConsume));

        if (IsExhausted || RemainingQuantity <= 0)
            throw new InvalidOperationException("Cannot consume from an exhausted cost layer.");

        if (quantityToConsume > RemainingQuantity)
            throw new InvalidOperationException($"Cannot consume {quantityToConsume} units from layer with only {RemainingQuantity} remaining.");

        decimal issueCost;

        if (quantityToConsume == RemainingQuantity)
        {
            // Final depletion of this layer: absorb all remaining cents to prevent penny drift
            issueCost = RemainingCost;
            RemainingQuantity = 0;
            RemainingCost = 0;
            IsExhausted = true;
        }
        else
        {
            issueCost = Math.Round(quantityToConsume * UnitCost, 2, MidpointRounding.AwayFromZero);
            RemainingQuantity -= quantityToConsume;
            RemainingCost -= issueCost;

            if (RemainingCost < 0)
            {
                RemainingCost = 0;
            }
        }

        return issueCost;
    }

    public decimal DeductForReversal(decimal reversalQuantity)
    {
        if (reversalQuantity <= 0)
            throw new ArgumentException("Reversal quantity must be greater than zero.", nameof(reversalQuantity));

        if (reversalQuantity > RemainingQuantity)
            throw new InvalidOperationException(
                $"Cannot reverse {reversalQuantity} units: only {RemainingQuantity} unconsumed units remain in this receipt layer. " +
                "Consumed stock cannot be returned to supplier.");

        decimal deductedCost;

        if (reversalQuantity == RemainingQuantity)
        {
            deductedCost = RemainingCost;
            RemainingQuantity = 0;
            RemainingCost = 0;
            IsExhausted = true;
        }
        else
        {
            deductedCost = Math.Round(reversalQuantity * UnitCost, 2, MidpointRounding.AwayFromZero);
            RemainingQuantity -= reversalQuantity;
            RemainingCost -= deductedCost;

            if (RemainingCost < 0)
            {
                RemainingCost = 0;
            }
        }

        return deductedCost;
    }

    private InventoryCostLayer()
    {
    }
}
