using System;
using System.Collections.Generic;

namespace ERP.Domain.Features.Inventory;

public enum StockIssuePurpose
{
    InternalUse = 1,        // Internal consumption / Maintenance / Scrap
    SalesDispatch = 2,      // Sales order fulfillment dispatch
    Manufacturing = 3,      // Production / Material issue
    Other = 4               // Other authorized purpose
}

public enum StockIssueStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public class InventorySummaryDto
{
    public int TotalStockItems { get; set; }
    public int TotalWarehouses { get; set; }
    public int TotalLocations { get; set; }
    public int LowStockItems { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalOnHandQuantity { get; set; }
    public decimal TotalInventoryValue { get; set; }
}

public class InventoryRecentTransactionDto
{
    public Guid InventoryTransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public int MovementTypeCode { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockIssueDto
{
    public Guid StockIssueId { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public StockIssuePurpose Purpose { get; set; }
    public string PurposeName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public StockIssueStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AvailableStockDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityQuarantine { get; set; }
    public decimal QuantityAvailable { get; set; }
}

public class CreateStockIssueRequest
{
    public Guid? StockIssueId { get; set; }
    public DateTime? IssueDate { get; set; }
    public StockIssuePurpose Purpose { get; set; } = StockIssuePurpose.InternalUse;
    public Guid WarehouseId { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public List<CreateStockIssueLineRequest> Lines { get; set; } = new();
}

public class CreateStockIssueLineRequest
{
    public Guid? StockIssueLineId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Remarks { get; set; }
}

public enum StockReceiptReason
{
    OpeningBalance = 1,
    CustomerReturn = 2,
    InternalProduction = 3,
    Miscellaneous = 4
}

public enum StockReceiptStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

public class StockReceiptDto
{
    public Guid StockReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public StockReceiptReason Reason { get; set; }
    public string ReasonName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public StockReceiptStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStockReceiptRequest
{
    public DateTime? ReceiptDate { get; set; }
    public StockReceiptReason Reason { get; set; } = StockReceiptReason.OpeningBalance;
    public Guid WarehouseId { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public List<CreateStockReceiptLineRequest> Lines { get; set; } = new();
}

public class CreateStockReceiptLineRequest
{
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Remarks { get; set; }
}
