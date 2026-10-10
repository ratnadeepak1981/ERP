using System;

namespace ERP.Domain.Features.Inventory;

public class PostInventoryMovementRequest
{
    public InventoryMovementType MovementType { get; set; }
    public SourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }
    public int MovementSequence { get; set; } = 1;

    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; } = 0;

    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? TransactionDate { get; set; }

    // QC flag: If true on an inflow, sets both QuantityOnHand and QuantityQuarantine
    public bool IsQuarantine { get; set; } = false;
}

public class PostTransferMovementRequest
{
    public SourceDocumentType SourceDocumentType { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceDocumentLineId { get; set; }

    public Guid ProductId { get; set; }

    // Source
    public Guid SourceWarehouseId { get; set; }
    public Guid SourceLocationId { get; set; }

    // Destination
    public Guid DestinationWarehouseId { get; set; }
    public Guid DestinationLocationId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; } = 0;
    public Guid? UnitOfMeasureId { get; set; }

    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? TransactionDate { get; set; }

    // If transferring out of quarantine (e.g. QC release to standard picking bin or transfer of quarantine stock)
    public bool IsSourceQuarantine { get; set; } = false;
    public bool IsDestinationQuarantine { get; set; } = false;
}

public class PostReversalRequest
{
    public Guid OriginalTransactionId { get; set; }
    public decimal? QuantityToReverse { get; set; } // If null, reverses remaining quantity
    public string? Reason { get; set; }
    public DateTime? TransactionDate { get; set; }
}
