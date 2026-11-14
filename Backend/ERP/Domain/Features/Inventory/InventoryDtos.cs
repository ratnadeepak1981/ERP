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
    public Guid? StockIssueLineId { get; set; }
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
    public Guid? StockReceiptLineId { get; set; }
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
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStockReceiptRequest
{
    public Guid? StockReceiptId { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public StockReceiptReason Reason { get; set; } = StockReceiptReason.OpeningBalance;
    public Guid WarehouseId { get; set; }
    public string? SourceDocumentReference { get; set; }
    public string? Remarks { get; set; }
    public List<CreateStockReceiptLineRequest> Lines { get; set; } = new();
}

public class CreateStockReceiptLineRequest
{
    public Guid? StockReceiptLineId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remarks { get; set; }
}

public class StockByLocationReportQuery
{
    public Guid? WarehouseId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? LocationClassification { get; set; } // "storage", "staging", or null/all
    public string? Search { get; set; }
    public int? Page { get; set; } = 1;
    public int? PageSize { get; set; } = 25;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "asc";
}

public class StockByLocationReportDto
{
    public List<StockByLocationRowDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public decimal TotalQuantityOnHand { get; set; }
    public decimal TotalQuantityAvailable { get; set; }
    public decimal TotalStockValue { get; set; }
    public string Currency { get; set; } = "LKR";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class StockByLocationRowDto
{
    public Guid InventoryBalanceId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;

    public Guid WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? ZoneCode { get; set; }
    public string? ZoneName { get; set; }
    public string? LocationTypeCode { get; set; }
    public string? LocationTypeName { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public string LocationClassification { get; set; } = string.Empty; // "Storage" or "Staging"

    public string BatchNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;

    public decimal QuantityOnHand { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityQuarantine { get; set; }
    public decimal QuantityAvailable { get; set; }

    public decimal UnitCost { get; set; }
    public decimal TotalCostValue { get; set; }
    public string ValuationMethod { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
}

public class StockLedgerReportQuery
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? WarehouseLocationId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
}

public class StockLedgerRowDto
{
    public Guid InventoryTransactionId { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime PostingDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int MovementTypeCode { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? SourceLocationCode { get; set; }
    public string? DestinationLocationCode { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }

    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal CostRate { get; set; }
    public decimal InwardValue { get; set; }
    public decimal OutwardValue { get; set; }
    public decimal RunningQuantity { get; set; }
    public decimal RunningValue { get; set; }
    public string ValuationMethod { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";
}

public class StockLedgerReportDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string HierarchyPath { get; set; } = string.Empty;
    public string ValuationMethod { get; set; } = string.Empty;
    public string Currency { get; set; } = "LKR";

    // Opening Balance (prior to DateFrom)
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningValue { get; set; }

    // Period Totals
    public decimal TotalQuantityIn { get; set; }
    public decimal TotalInwardValue { get; set; }
    public decimal TotalQuantityOut { get; set; }
    public decimal TotalOutwardValue { get; set; }

    // Closing Balance
    public decimal ClosingQuantity { get; set; }
    public decimal ClosingValue { get; set; }

    // Detail Items
    public List<StockLedgerRowDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
