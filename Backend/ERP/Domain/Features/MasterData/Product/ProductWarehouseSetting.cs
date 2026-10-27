using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Product;

public class ProductWarehouseSetting : Auditable, ITenantScopedEntity
{
    public Guid ProductWarehouseSettingId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }

    // Warehouse-specific overrides (nullable: null falls back to Product default)
    public decimal? ReorderLevel { get; private set; }
    public decimal? ReorderQuantity { get; private set; }
    public decimal? MinStockQuantity { get; private set; }
    public decimal? MaxStockQuantity { get; private set; }
    public decimal? SafetyStockQuantity { get; private set; }
    public int? LeadTimeDays { get; private set; }

    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public static ProductWarehouseSetting Create(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        decimal? reorderLevel = null,
        decimal? reorderQuantity = null,
        decimal? minStockQuantity = null,
        decimal? maxStockQuantity = null,
        decimal? safetyStockQuantity = null,
        int? leadTimeDays = null,
        Guid? productWarehouseSettingId = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));
        if (warehouseId == Guid.Empty)
            throw new ArgumentException("WarehouseId is required.", nameof(warehouseId));

        ValidateSettings(reorderLevel, reorderQuantity, minStockQuantity, maxStockQuantity, safetyStockQuantity, leadTimeDays);

        return new ProductWarehouseSetting
        {
            ProductWarehouseSettingId = productWarehouseSettingId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            ReorderLevel = reorderLevel,
            ReorderQuantity = reorderQuantity,
            MinStockQuantity = minStockQuantity,
            MaxStockQuantity = maxStockQuantity,
            SafetyStockQuantity = safetyStockQuantity,
            LeadTimeDays = leadTimeDays,
            IsActive = true
        };
    }

    public void Update(
        decimal? reorderLevel,
        decimal? reorderQuantity,
        decimal? minStockQuantity,
        decimal? maxStockQuantity,
        decimal? safetyStockQuantity,
        int? leadTimeDays)
    {
        ValidateSettings(reorderLevel, reorderQuantity, minStockQuantity, maxStockQuantity, safetyStockQuantity, leadTimeDays);

        ReorderLevel = reorderLevel;
        ReorderQuantity = reorderQuantity;
        MinStockQuantity = minStockQuantity;
        MaxStockQuantity = maxStockQuantity;
        SafetyStockQuantity = safetyStockQuantity;
        LeadTimeDays = leadTimeDays;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private static void ValidateSettings(
        decimal? reorderLevel,
        decimal? reorderQuantity,
        decimal? minStockQuantity,
        decimal? maxStockQuantity,
        decimal? safetyStockQuantity,
        int? leadTimeDays)
    {
        if (reorderLevel.HasValue && reorderLevel.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "Reorder level cannot be negative.");

        if (reorderQuantity.HasValue && reorderQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderQuantity), "Reorder quantity cannot be negative.");

        if (minStockQuantity.HasValue && minStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(minStockQuantity), "Minimum stock quantity cannot be negative.");

        if (maxStockQuantity.HasValue && maxStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(maxStockQuantity), "Maximum stock quantity cannot be negative.");

        if (safetyStockQuantity.HasValue && safetyStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(safetyStockQuantity), "Safety stock quantity cannot be negative.");

        if (leadTimeDays.HasValue && leadTimeDays.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(leadTimeDays), "Lead time days cannot be negative.");

        if (minStockQuantity.HasValue && maxStockQuantity.HasValue && minStockQuantity.Value > maxStockQuantity.Value)
            throw new InvalidOperationException("Minimum stock quantity cannot be greater than maximum stock quantity.");

        if (safetyStockQuantity.HasValue && maxStockQuantity.HasValue && safetyStockQuantity.Value > maxStockQuantity.Value)
            throw new InvalidOperationException("Safety stock quantity cannot be greater than maximum stock quantity.");

        if (safetyStockQuantity.HasValue && minStockQuantity.HasValue && safetyStockQuantity.Value > minStockQuantity.Value)
            throw new InvalidOperationException("Safety stock quantity cannot be greater than minimum stock quantity.");
    }

    private ProductWarehouseSetting()
    {
    }
}
