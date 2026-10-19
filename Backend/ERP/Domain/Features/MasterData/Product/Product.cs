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
        Guid? unitOfMeasureId = null)
    {
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
        Guid? unitOfMeasureId = null)
    {
        ProductName = productName;
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure;
        Description = description;
        ValuationMethod = valuationMethod;
        StandardCost = standardCost;
        SellingPrice = sellingPrice;
        ReorderLevel = reorderLevel;
        ReorderQuantity = reorderQuantity;
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