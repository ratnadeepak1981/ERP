using ERP.Domain.Common;

namespace ERP.Domain.Features.MasterData.Product;

public class Product : Auditable
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