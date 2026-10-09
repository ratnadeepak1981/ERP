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
        Guid? productId = null)
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
            IsActive = true
        };
    }

    private Product()
    {
    }
  
}
public enum ValuationMethod
{
    FIFO = 1,
    WeightedAverage = 2,
    StandardCost = 3
}