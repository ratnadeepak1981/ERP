using System;
using System.Collections.Generic;

namespace ERP.Application.Features.Inventory.Replenishment;

public enum ReplenishmentSourceType
{
    Manual = 0,
    SalesOrder = 1,
    SalesShipment = 2,
    InventoryPlanning = 3
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
    public decimal QuantityOnOrder { get; set; }
    public decimal NetAvailableStock { get; set; }
    public decimal ShortageQuantity { get; set; }
    public bool HasShortage { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal? EstimatedUnitPrice { get; set; }
}

public class ReplenishmentAssessmentResult
{
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public List<ReplenishmentLineAssessmentDto> Lines { get; set; } = new();
    public bool HasAnyShortage { get; set; }
    public int TotalLinesWithShortage { get; set; }
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
