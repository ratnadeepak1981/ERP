using System;
using System.Collections.Generic;

namespace ERP.Domain.Features.Inventory.StockCount;

public class CreateStockCountDraftRequest
{
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public Guid CompanyId { get; set; }
    public string? CountNumber { get; set; }
    public DateTime? CountDate { get; set; }
    public string? Remarks { get; set; }
    public List<CreateStockCountLineRequest> Lines { get; set; } = new();
}

public class CreateStockCountLineRequest
{
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    // Retained for API compatibility; expected quantities are read from inventory.
    public decimal ExpectedQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public string? Remarks { get; set; }
}

public class UpdateStockCountDraftRequest
{
    public string? CountNumber { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? WarehouseLocationId { get; set; }
    public DateTime? CountDate { get; set; }
    public string? Remarks { get; set; }
    public List<UpdateStockCountLineRequest> Lines { get; set; } = new();
}

public class UpdateStockCountLineRequest
{
    public Guid StockCountLineId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    // Retained for API compatibility; expected quantities are immutable snapshots.
    public decimal ExpectedQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public string? Remarks { get; set; }
}

public class SubmitStockCountRequest
{
    public Guid SubmittedByUserId { get; set; }
    public string? Notes { get; set; }
}

public class ApproveStockCountRequest
{
    public Guid ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
}

public class PostStockCountRequest
{
    public Guid PostedByUserId { get; set; }
    public string? Notes { get; set; }
}

public class CancelStockCountRequest
{
    public Guid CancelledByUserId { get; set; }
    public string? Reason { get; set; }
}

public class StockCountDto
{
    public Guid StockCountId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public DateTime CountDate { get; set; }
    public StockCountStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Remarks { get; set; }
    public decimal TotalExpectedQuantity { get; set; }
    public decimal TotalPhysicalQuantity { get; set; }
    public decimal TotalVarianceQuantity { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<StockCountLineDto> Lines { get; set; } = new();
}

public class StockCountLineDto
{
    public Guid StockCountLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid UnitOfMeasureId { get; set; }
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public decimal ExpectedQuantityBase { get; set; }
    public decimal PhysicalQuantityBase { get; set; }
    public decimal VarianceQuantityBase { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }
    public List<StockCountInventoryTransactionDto> InventoryTransactions { get; set; } = new();
    public string? Remarks { get; set; }
}

public class StockCountInventoryTransactionDto
{
    public Guid InventoryTransactionId { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public SourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid SourceDocumentLineId { get; set; }
    public int MovementSequence { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime TransactionDate { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
}
