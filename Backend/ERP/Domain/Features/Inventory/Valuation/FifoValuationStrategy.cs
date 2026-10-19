using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public class FifoValuationStrategy : IInventoryValuationStrategy
{
    private readonly IInventoryRepository _inventoryRepository;

    public FifoValuationStrategy(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public ValuationMethod Method => ValuationMethod.FIFO;

    public async Task<ValuationInflowResult> ProcessInflowAsync(
        ValuationContext context,
        decimal quantity,
        decimal unitCost,
        Guid transactionId)
    {
        if (quantity <= 0)
            throw new ArgumentException("Inflow quantity must be greater than zero.", nameof(quantity));

        var layer = InventoryCostLayer.Create(
            context.TenantId,
            context.ProductId,
            context.WarehouseId,
            context.WarehouseLocationId,
            transactionId,
            context.TransactionDate,
            quantity,
            unitCost,
            context.BatchNumber,
            context.SerialNumber);

        await _inventoryRepository.AddCostLayerAsync(layer);

        return new ValuationInflowResult
        {
            UnitCost = unitCost,
            TotalCost = layer.OriginalCost,
            VarianceAmount = 0
        };
    }

    public async Task<ValuationOutflowResult> ProcessOutflowAsync(
        ValuationContext context,
        decimal quantity,
        Guid transactionId)
    {
        if (quantity <= 0)
            throw new ArgumentException("Outflow quantity must be greater than zero.", nameof(quantity));

        var layers = await _inventoryRepository.GetActiveCostLayersAsync(
            context.TenantId,
            context.ProductId,
            context.WarehouseId,
            ascending: true,
            context.BatchNumber,
            context.SerialNumber);

        decimal availableQuantity = layers.Sum(x => x.RemainingQuantity);
        if (availableQuantity < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient valuation cost layers for product '{context.Product.ProductCode}'. " +
                $"Requested: {quantity}, Available: {availableQuantity}.");
        }

        decimal remainingToConsume = quantity;
        decimal totalIssueCost = 0;
        var consumedLayers = new List<(Guid CostLayerId, decimal QuantityConsumed, decimal CostConsumed)>();

        foreach (var layer in layers)
        {
            if (layer.IsExhausted || layer.RemainingQuantity <= 0) continue;

            decimal toConsume = Math.Min(layer.RemainingQuantity, remainingToConsume);
            decimal layerCost = layer.Consume(toConsume);

            totalIssueCost += layerCost;
            remainingToConsume -= toConsume;
            consumedLayers.Add((layer.CostLayerId, toConsume, layerCost));

            if (remainingToConsume <= 0) break;
        }

        decimal effectiveUnitCost = Math.Round(totalIssueCost / quantity, 4, MidpointRounding.AwayFromZero);

        return new ValuationOutflowResult
        {
            UnitCost = effectiveUnitCost,
            TotalCost = totalIssueCost,
            ConsumedLayers = consumedLayers
        };
    }

    public async Task<ValuationReversalResult> ProcessInflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalInflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        var layer = await _inventoryRepository.GetCostLayerByTransactionIdAsync(
            context.TenantId,
            originalInflowTxn.InventoryTransactionId);

        if (layer == null)
        {
            throw new InvalidOperationException(
                $"Original cost layer for receipt transaction '{originalInflowTxn.TransactionNumber}' not found.");
        }

        decimal deductedCost = layer.DeductForReversal(reversalQuantity);

        return new ValuationReversalResult
        {
            ReversalUnitCost = layer.UnitCost,
            ReversalTotalCost = deductedCost,
            VarianceAmount = 0
        };
    }

    public async Task<ValuationReversalResult> ProcessOutflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalOutflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        // Outflow reversal restores stock at the original historical issue unit cost
        decimal unitCost = originalOutflowTxn.UnitCost;

        var restoredLayer = InventoryCostLayer.Create(
            context.TenantId,
            context.ProductId,
            context.WarehouseId,
            context.WarehouseLocationId,
            reversalTxnId,
            context.TransactionDate,
            reversalQuantity,
            unitCost,
            context.BatchNumber,
            context.SerialNumber);

        await _inventoryRepository.AddCostLayerAsync(restoredLayer);

        return new ValuationReversalResult
        {
            ReversalUnitCost = unitCost,
            ReversalTotalCost = restoredLayer.OriginalCost,
            VarianceAmount = 0
        };
    }
}
