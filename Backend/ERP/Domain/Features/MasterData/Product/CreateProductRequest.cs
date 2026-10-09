namespace Domain.Features.MasterData.Product;

public class CreateProductRequest
{
    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string? Description { get; set; }

    public string UnitOfMeasure { get; set; } = "PCS";

    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.FIFO;

    public decimal StandardCost { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal ReorderLevel { get; set; }

    public decimal ReorderQuantity { get; set; }

    public bool IsManufacturable { get; set; }

    public bool IsPurchasable { get; set; } = true;

    public bool IsSellable { get; set; } = true;
}
