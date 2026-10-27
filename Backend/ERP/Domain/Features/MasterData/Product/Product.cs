using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.Product;

public class Product : Auditable, ITenantScopedEntity
{
    public Guid ProductId { get; private set; }

    public Guid TenantId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    public string? Description { get; private set; }

    public string UnitOfMeasure { get; private set; } = string.Empty;

    // Business capabilities
    public bool IsManufacturable { get; private set; }

    public bool IsPurchasable { get; private set; }

    public bool IsSellable { get; private set; }

    public bool CanConsumeInProduction { get; private set; }

    public bool CanConsumeInMaintenance { get; private set; }

    public bool IsStockTracked { get; private set; } = true;

    public ProductType ProductType { get; private set; } = ProductType.StockItem;

    public TrackingMode TrackingMode { get; private set; } = TrackingMode.None;

    public Guid? UnitOfMeasureId { get; private set; }

    // Inventory / costing
    public ValuationMethod ValuationMethod { get; private set; }

    public decimal StandardCost { get; private set; }

    public decimal SellingPrice { get; private set; }

    public decimal ReorderLevel { get; private set; }

    public decimal ReorderQuantity { get; private set; }

    public decimal? MinStockQuantity { get; private set; }

    public decimal? MaxStockQuantity { get; private set; }

    public decimal? SafetyStockQuantity { get; private set; }

    public int? LeadTimeDays { get; private set; }

    public bool IsActive { get; private set; }

    public static Product Create(
        Guid tenantId,
        string productCode,
        string productName,
        Guid categoryId,
        string unitOfMeasure = "PCS",
        string? description = null,
        ValuationMethod valuationMethod = ValuationMethod.FIFO,
        decimal standardCost = 0,
        decimal sellingPrice = 0,
        decimal reorderLevel = 0,
        decimal reorderQuantity = 0,
        bool isManufacturable = false,
        bool isPurchasable = true,
        bool isSellable = true,
        Guid? productId = null,
        bool canConsumeInProduction = false,
        bool canConsumeInMaintenance = false,
        bool isStockTracked = true,
        ProductType productType = ProductType.StockItem,
        TrackingMode trackingMode = TrackingMode.None,
        Guid? unitOfMeasureId = null,
        decimal? minStockQuantity = null,
        decimal? maxStockQuantity = null,
        decimal? safetyStockQuantity = null,
        int? leadTimeDays = null)
    {
        ValidatePlanningSettings(reorderLevel, reorderQuantity, minStockQuantity, maxStockQuantity, safetyStockQuantity, leadTimeDays);

        return new Product
        {
            ProductId = productId ?? Guid.NewGuid(),
            TenantId = tenantId,
            ProductCode = productCode,
            ProductName = productName,
            CategoryId = categoryId,
            UnitOfMeasure = unitOfMeasure,
            Description = description,
            ValuationMethod = valuationMethod,
            StandardCost = standardCost,
            SellingPrice = sellingPrice,
            ReorderLevel = reorderLevel,
            ReorderQuantity = reorderQuantity,
            MinStockQuantity = minStockQuantity,
            MaxStockQuantity = maxStockQuantity,
            SafetyStockQuantity = safetyStockQuantity,
            LeadTimeDays = leadTimeDays,
            IsManufacturable = isManufacturable,
            IsPurchasable = isPurchasable,
            IsSellable = isSellable,
            CanConsumeInProduction = canConsumeInProduction,
            CanConsumeInMaintenance = canConsumeInMaintenance,
            IsStockTracked = isStockTracked,
            ProductType = productType,
            TrackingMode = trackingMode,
            UnitOfMeasureId = unitOfMeasureId,
            IsActive = true
        };
    }

    public void Update(
        string productName,
        Guid categoryId,
        string unitOfMeasure,
        string? description,
        ValuationMethod valuationMethod,
        decimal standardCost,
        decimal sellingPrice,
        decimal reorderLevel,
        decimal reorderQuantity,
        bool isManufacturable,
        bool isPurchasable,
        bool isSellable,
        bool canConsumeInProduction,
        bool canConsumeInMaintenance,
        bool isStockTracked,
        ProductType productType,
        TrackingMode trackingMode,
        Guid? unitOfMeasureId = null,
        decimal? minStockQuantity = null,
        decimal? maxStockQuantity = null,
        decimal? safetyStockQuantity = null,
        int? leadTimeDays = null)
    {
        ValidatePlanningSettings(reorderLevel, reorderQuantity, minStockQuantity, maxStockQuantity, safetyStockQuantity, leadTimeDays);

        ProductName = productName;
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure;
        Description = description;
        ValuationMethod = valuationMethod;
        StandardCost = standardCost;
        SellingPrice = sellingPrice;
        ReorderLevel = reorderLevel;
        ReorderQuantity = reorderQuantity;
        MinStockQuantity = minStockQuantity;
        MaxStockQuantity = maxStockQuantity;
        SafetyStockQuantity = safetyStockQuantity;
        LeadTimeDays = leadTimeDays;
        IsManufacturable = isManufacturable;
        IsPurchasable = isPurchasable;
        IsSellable = isSellable;
        CanConsumeInProduction = canConsumeInProduction;
        CanConsumeInMaintenance = canConsumeInMaintenance;
        IsStockTracked = isStockTracked;
        ProductType = productType;
        TrackingMode = trackingMode;
        UnitOfMeasureId = unitOfMeasureId;
    }

    public void UpdatePlanningSettings(
        decimal reorderLevel,
        decimal reorderQuantity,
        decimal? minStockQuantity = null,
        decimal? maxStockQuantity = null,
        decimal? safetyStockQuantity = null,
        int? leadTimeDays = null)
    {
        ValidatePlanningSettings(reorderLevel, reorderQuantity, minStockQuantity, maxStockQuantity, safetyStockQuantity, leadTimeDays);

        ReorderLevel = reorderLevel;
        ReorderQuantity = reorderQuantity;
        MinStockQuantity = minStockQuantity;
        MaxStockQuantity = maxStockQuantity;
        SafetyStockQuantity = safetyStockQuantity;
        LeadTimeDays = leadTimeDays;
    }

    public EffectivePlanningSettings GetEffectivePlanningSettings(ProductWarehouseSetting? warehouseSetting = null)
    {
        if (warehouseSetting == null || !warehouseSetting.IsActive || warehouseSetting.ProductId != ProductId)
        {
            return new EffectivePlanningSettings(
                ReorderLevel: ReorderLevel,
                ReorderQuantity: ReorderQuantity,
                MinStockQuantity: MinStockQuantity,
                MaxStockQuantity: MaxStockQuantity,
                SafetyStockQuantity: SafetyStockQuantity,
                LeadTimeDays: LeadTimeDays,
                IsWarehouseOverrideApplied: false,
                WarehouseId: null);
        }

        return new EffectivePlanningSettings(
            ReorderLevel: warehouseSetting.ReorderLevel ?? ReorderLevel,
            ReorderQuantity: warehouseSetting.ReorderQuantity ?? ReorderQuantity,
            MinStockQuantity: warehouseSetting.MinStockQuantity ?? MinStockQuantity,
            MaxStockQuantity: warehouseSetting.MaxStockQuantity ?? MaxStockQuantity,
            SafetyStockQuantity: warehouseSetting.SafetyStockQuantity ?? SafetyStockQuantity,
            LeadTimeDays: warehouseSetting.LeadTimeDays ?? LeadTimeDays,
            IsWarehouseOverrideApplied: true,
            WarehouseId: warehouseSetting.WarehouseId);
    }

    private static void ValidatePlanningSettings(
        decimal reorderLevel,
        decimal reorderQuantity,
        decimal? minStockQuantity,
        decimal? maxStockQuantity,
        decimal? safetyStockQuantity,
        int? leadTimeDays)
    {
        if (reorderLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "Reorder level cannot be negative.");

        if (reorderQuantity < 0)
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

    public void SetUnitOfMeasure(Guid? unitOfMeasureId, string unitOfMeasureCode)
    {
        UnitOfMeasureId = unitOfMeasureId;
        UnitOfMeasure = unitOfMeasureCode;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private Product()
    {
    }
}

public enum ValuationMethod
{
    FIFO = 1,
    WeightedAverage = 2,
    StandardCost = 3,
    LIFO = 4
}

public enum ProductType
{
    StockItem = 1,
    NonStockItem = 2,
    Service = 3
}

public enum TrackingMode
{
    None = 0,
    Batch = 1,
    Serial = 2,
    BatchAndSerial = 3
}
