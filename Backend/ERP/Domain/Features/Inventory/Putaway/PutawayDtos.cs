using System;
using System.Collections.Generic;

namespace Domain.Features.Inventory.Putaway;

public class PutawayDto
{
    public Guid PutawayId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BranchId { get; set; }

    public string PutawayNumber { get; set; } = string.Empty;
    public DateTime PutawayDate { get; set; }

    public PutawaySourceDocumentType SourceDocumentType { get; set; }
    public string SourceDocumentTypeName { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;

    public PutawayStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public int TotalLines { get; set; }
    public decimal TotalQuantity { get; set; }

    public DateTime CreatedAt { get; set; }
    public List<PutawayLineDto> Lines { get; set; } = new();
}

public class PutawayLineDto
{
    public Guid PutawayLineId { get; set; }
    public Guid PutawayId { get; set; }

    public Guid? SourceDocumentLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid SourceWarehouseLocationId { get; set; }
    public string SourceLocationCode { get; set; } = string.Empty;

    public decimal ReceivedQuantity { get; set; }
    public decimal PreviouslyPutawayQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
    public decimal PutawayQuantity { get; set; }

    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public List<PutawayLineAllocationDto> Allocations { get; set; } = new();
}

public class PutawayLineAllocationDto
{
    public Guid PutawayLineAllocationId { get; set; }
    public Guid PutawayLineId { get; set; }

    public Guid DestinationWarehouseLocationId { get; set; }
    public string DestinationLocationCode { get; set; } = string.Empty;
    public string DestinationLocationName { get; set; } = string.Empty;
    public decimal AllocatedQuantity { get; set; }
    public int MovementSequence { get; set; }

    public Guid? InventoryTransactionId { get; set; }
}

public class CreatePutawayDraftRequest
{
    public DateTime? PutawayDate { get; set; }
    public PutawaySourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string? SourceDocumentNumber { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public List<CreatePutawayLineRequest> Lines { get; set; } = new();
}

public class CreatePutawayLineRequest
{
    public Guid? SourceDocumentLineId { get; set; }
    public Guid ProductId { get; set; }
    public Guid SourceWarehouseLocationId { get; set; }
    public Guid? DestinationWarehouseLocationId { get; set; }
    public Guid? DestinationLocationId { get => DestinationWarehouseLocationId; set => DestinationWarehouseLocationId = value; }
    public Guid? UnitOfMeasureId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal PutawayQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public List<CreatePutawayAllocationRequest> Allocations { get; set; } = new();
}

public class CreatePutawayAllocationRequest
{
    public Guid DestinationWarehouseLocationId { get; set; }
    public decimal AllocatedQuantity { get; set; }
}

public class UpdatePutawayDraftRequest
{
    public DateTime? PutawayDate { get; set; }
    public string? Notes { get; set; }
    public List<CreatePutawayLineRequest> Lines { get; set; } = new();
}

public class EligibleSourceReceiptDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public PutawaySourceDocumentType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int TotalLines { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
}

public class EligibleSourceReceiptLineDto
{
    public Guid? SourceDocumentLineId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid? UnitOfMeasureId { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid SourceWarehouseLocationId { get; set; }
    public string SourceLocationCode { get; set; } = string.Empty;
    public string SourceLocationName { get; set; } = string.Empty;
    public decimal ReceivedQuantity { get; set; }
    public decimal PreviouslyPutawayQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
