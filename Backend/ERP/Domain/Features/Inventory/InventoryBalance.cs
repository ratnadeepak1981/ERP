using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace ERP.Domain.Features.Inventory;

public class InventoryBalance : Auditable, ITenantScopedEntity
{
    public Guid InventoryBalanceId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid WarehouseLocationId { get; private set; }

    public Guid ProductId { get; private set; }

    // Normalized as empty strings if untracked to support SQL Server UNIQUE index cleanly
    public string BatchNumber { get; private set; } = string.Empty;

    public string SerialNumber { get; private set; } = string.Empty;

    // Physical & Logical Quantities (Must never be negative)
    public decimal QuantityOnHand { get; private set; }

    public decimal QuantityAllocated { get; private set; }

    public decimal QuantityQuarantine { get; private set; }

    public decimal QuantityOnOrder { get; private set; }

    // Optimistic Concurrency Token
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Computed Available Quantity: Physical on-hand minus allocated minus quarantined
    public decimal QuantityAvailable => QuantityOnHand - QuantityAllocated - QuantityQuarantine;

    public static InventoryBalance Create(
        Guid tenantId,
        Guid warehouseId,
        Guid warehouseLocationId,
        Guid productId,
        string? batchNumber = null,
        string? serialNumber = null,
        Guid? inventoryBalanceId = null)
    {
        return new InventoryBalance
        {
            InventoryBalanceId = inventoryBalanceId ?? Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            WarehouseLocationId = warehouseLocationId,
            ProductId = productId,
            BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? string.Empty : batchNumber.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? string.Empty : serialNumber.Trim(),
            QuantityOnHand = 0,
            QuantityAllocated = 0,
            QuantityQuarantine = 0,
            QuantityOnOrder = 0
        };
    }

    public void ApplyOnHandChange(decimal deltaQuantity)
    {
        decimal newOnHand = QuantityOnHand + deltaQuantity;
        if (newOnHand < 0)
        {
            throw new InvalidOperationException($"Cannot reduce on-hand stock below zero. Current: {QuantityOnHand}, Delta: {deltaQuantity}.");
        }

        QuantityOnHand = newOnHand;
    }

    public void ApplyQuarantineChange(decimal deltaQuarantine)
    {
        decimal newQuarantine = QuantityQuarantine + deltaQuarantine;
        if (newQuarantine < 0)
        {
            throw new InvalidOperationException($"Cannot reduce quarantine stock below zero. Current: {QuantityQuarantine}, Delta: {deltaQuarantine}.");
        }

        if (newQuarantine > QuantityOnHand)
        {
            throw new InvalidOperationException($"Quarantine stock ({newQuarantine}) cannot exceed total on-hand stock ({QuantityOnHand}).");
        }

        QuantityQuarantine = newQuarantine;
    }

    public void ApplyAllocationChange(decimal deltaAllocated)
    {
        decimal newAllocated = QuantityAllocated + deltaAllocated;
        if (newAllocated < 0)
        {
            throw new InvalidOperationException($"Cannot reduce allocated stock below zero. Current: {QuantityAllocated}, Delta: {deltaAllocated}.");
        }

        if (newAllocated + QuantityQuarantine > QuantityOnHand)
        {
            throw new InvalidOperationException($"Allocated ({newAllocated}) plus quarantine stock ({QuantityQuarantine}) cannot exceed on-hand stock ({QuantityOnHand}).");
        }

        QuantityAllocated = newAllocated;
    }

    public void ApplyOnOrderChange(decimal deltaOnOrder)
    {
        decimal newOnOrder = QuantityOnOrder + deltaOnOrder;
        if (newOnOrder < 0)
        {
            throw new InvalidOperationException($"Cannot reduce on-order stock below zero. Current: {QuantityOnOrder}, Delta: {deltaOnOrder}.");
        }

        QuantityOnOrder = newOnOrder;
    }

    private InventoryBalance()
    {
    }
}
