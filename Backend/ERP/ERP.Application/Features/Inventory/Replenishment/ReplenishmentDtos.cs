using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Application.Features.Inventory.Replenishment;

public enum ReplenishmentSourceType
{
    Manual = 0,
    SalesOrder = 1,
    SalesShipment = 2,
    InventoryPlanning = 3
}

public enum ReplenishmentAssessmentMethod
{
    Manual = 0,
    ConfirmedSalesOrder = 1,
    ReorderLevel = 2,
    MinimumStock = 3,
    MaximumStock = 4
}

public class ReplenishmentItemDemandDto
{
    public Guid ProductId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string? Remarks { get; set; }
}

public class ReplenishmentAssessmentRequest
{
    public Guid WarehouseId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public ReplenishmentAssessmentMethod? Method { get; set; }
    public Guid? SalesOrderId { get; set; }
    public List<ReplenishmentItemDemandDto> Items { get; set; } = new();
}

public class ReplenishmentLineAssessmentDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ExpiredQuantity { get; set; }
    public decimal EligibleOnHandQuantity { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityQuarantine { get; set; }
    public decimal UncommittedStockQuantity { get; set; }
    public decimal QuantityOnOrder { get; set; }
    public decimal NetAvailableStock { get; set; }
    public decimal StockPosition { get; set; }
    public decimal ShortageQuantity { get; set; }
    public bool HasShortage { get; set; }
    public decimal? ReorderLevel { get; set; }
    public decimal? ReorderQuantity { get; set; }
    public decimal? MinStockLevel { get; set; }
    public decimal? MaxStockLevel { get; set; }
    public bool IsTriggered { get; set; }
    public string? CalculationDetails { get; set; }
    public string? WarningMessage { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal? EstimatedUnitPrice { get; set; }
}

public class ReplenishmentAssessmentResult
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public ReplenishmentAssessmentMethod? Method { get; set; }
    public List<ReplenishmentLineAssessmentDto> Lines { get; set; } = new();
    public bool HasAnyShortage { get; set; }
    public int TotalLinesAssessed => Lines.Count;
    public int LinesWithShortage => Lines.Count(l => l.HasShortage);
    public int TotalLinesWithShortage { get => LinesWithShortage; set { } }
}

public class ReplenishmentPrLineRequest
{
    public Guid ProductId { get; set; }
    public decimal? RequestedQuantity { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public decimal? EstimatedUnitPrice { get; set; }
    public string? Remarks { get; set; }
}

public class ReplenishmentPrItemRequestDto : ReplenishmentPrLineRequest
{
}

public class CreateReplenishmentPrRequest
{
    public Guid? WarehouseId { get; set; }
    public string? RequisitionNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Notes { get; set; }
    public ReplenishmentSourceType SourceType { get; set; } = ReplenishmentSourceType.Manual;
    public Guid? SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public Guid? SourceEntityId { get => SourceDocumentId; set => SourceDocumentId = value; }
    public string? SourceReference { get => SourceDocumentNumber; set => SourceDocumentNumber = value; }
    public List<ReplenishmentPrLineRequest> Items { get; set; } = new();
}

public class DraftPrLineResultDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ShortageQuantity { get; set; }
    public decimal RequisitionQuantity { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
    public decimal EstimatedLineTotal { get; set; }
}

public class DraftPurchaseRequisitionResult
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime RequisitionDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public int ItemCount { get; set; }
    public int LineCount { get => ItemCount; set => ItemCount = value; }
    public decimal TotalAmount { get; set; }
    public List<DraftPrLineResultDto> Items { get; set; } = new();
    public bool IsReused { get; set; }
    public string Message { get; set; } = string.Empty;
}
