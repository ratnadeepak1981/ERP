using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public class InventoryValuationService : IInventoryValuationService
{
    private readonly IEnumerable<IInventoryValuationStrategy> _strategies;
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryValuationService(
        IEnumerable<IInventoryValuationStrategy> strategies,
        IInventoryRepository inventoryRepository)
    {
        _strategies = strategies;
        _inventoryRepository = inventoryRepository;
    }

    private IInventoryValuationStrategy GetStrategy(ValuationMethod method)
    {
        var strategy = _strategies.FirstOrDefault(s => s.Method == method);
        if (strategy == null)
        {
            throw new NotSupportedException($"No valuation strategy registered for valuation method: {method}");
        }

        return strategy;
    }

    public async Task AcquireValuationLockAsync(Guid tenantId, Guid productId, Guid warehouseId)
    {
        await _inventoryRepository.AcquireValuationLockAsync(tenantId, productId, warehouseId);
    }

    public async Task AcquireTransferValuationLocksAsync(Guid tenantId, Guid productId, Guid sourceWarehouseId, Guid destinationWarehouseId)
    {
        await _inventoryRepository.AcquireTransferValuationLocksAsync(tenantId, productId, sourceWarehouseId, destinationWarehouseId);
    }

    public async Task<ValuationInflowResult> ProcessInflowAsync(
        ValuationContext context,
        decimal quantity,
        decimal unitCost,
        Guid transactionId)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.Product == null) throw new ArgumentException("Product must be provided in ValuationContext.", nameof(context));

        var strategy = GetStrategy(context.Product.ValuationMethod);
        return await strategy.ProcessInflowAsync(context, quantity, unitCost, transactionId);
    }

    public async Task<ValuationOutflowResult> ProcessOutflowAsync(
        ValuationContext context,
        decimal quantity,
        Guid transactionId)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.Product == null) throw new ArgumentException("Product must be provided in ValuationContext.", nameof(context));

        var strategy = GetStrategy(context.Product.ValuationMethod);
        return await strategy.ProcessOutflowAsync(context, quantity, transactionId);
    }

    public async Task<ValuationReversalResult> ProcessInflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalInflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.Product == null) throw new ArgumentException("Product must be provided in ValuationContext.", nameof(context));

        var strategy = GetStrategy(context.Product.ValuationMethod);
        return await strategy.ProcessInflowReversalAsync(context, originalInflowTxn, reversalQuantity, reversalTxnId);
    }

    public async Task<ValuationReversalResult> ProcessOutflowReversalAsync(
        ValuationContext context,
        InventoryTransaction originalOutflowTxn,
        decimal reversalQuantity,
        Guid reversalTxnId)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.Product == null) throw new ArgumentException("Product must be provided in ValuationContext.", nameof(context));

        var strategy = GetStrategy(context.Product.ValuationMethod);
        return await strategy.ProcessOutflowReversalAsync(context, originalOutflowTxn, reversalQuantity, reversalTxnId);
    }
}
