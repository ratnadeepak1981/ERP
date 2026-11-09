using System;

namespace ERP.Domain.Features.Inventory.StockCount;

public class StockCountLine
{
    public Guid StockCountLineId { get; set; }
    public Guid StockCountId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public decimal ExpectedQuantityBase { get; set; }
    public decimal PhysicalQuantityBase { get; set; }
    public decimal VarianceQuantityBase { get; set; }
    public decimal ExpectedQuantityEntered { get; set; }
    public decimal PhysicalQuantityEntered { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static StockCountLine Create(
        Guid productId,
        Guid warehouseLocationId,
        Guid unitOfMeasureId,
        string productCode,
        string productName,
        string unitOfMeasureCode,
        decimal expectedQuantityBase,
        decimal physicalQuantityBase,
        string? batchNumber = null,
        string? serialNumber = null,
        DateTime? manufacturingDate = null,
        DateTime? expiryDate = null,
        decimal unitCost = 0m,
        string? remarks = null)
    {
        var line = new StockCountLine
        {
            StockCountLineId = Guid.NewGuid(),
            ProductId = productId,
            WarehouseLocationId = warehouseLocationId,
            ProductCode = productCode,
            ProductName = productName,
            UnitOfMeasureId = unitOfMeasureId,
            UnitOfMeasureCode = unitOfMeasureCode,
            ExpectedQuantityBase = expectedQuantityBase,
            PhysicalQuantityBase = physicalQuantityBase,
            ExpectedQuantityEntered = expectedQuantityBase,
            PhysicalQuantityEntered = physicalQuantityBase,
            BatchNumber = batchNumber,
            SerialNumber = serialNumber,
            ManufacturingDate = manufacturingDate,
            ExpiryDate = expiryDate,
            UnitCost = unitCost,
            Remarks = remarks,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        line.RecalculateVariance();
        return line;
    }

    public void RecalculateVariance()
    {
        VarianceQuantityBase = PhysicalQuantityBase - ExpectedQuantityBase;
        UpdatedAt = DateTime.UtcNow;
    }
}
