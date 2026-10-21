using System;
using System.Collections.Generic;
using ERP.Domain.Common;

namespace Domain.Features.Inventory.Putaway;

public class PutawayLine : Auditable
{
    public Guid PutawayLineId { get; set; }
    public Guid PutawayId { get; set; }

    public Guid? SourceDocumentLineId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }

    public Guid SourceWarehouseLocationId { get; set; }

    public decimal ReceivedQuantity { get; set; }
    public decimal PreviouslyPutawayQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
    public decimal PutawayQuantity { get; set; }

    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public List<PutawayLineAllocation> Allocations { get; set; } = new();

    public static PutawayLine Create(
        Guid putawayId,
        Guid? sourceDocumentLineId,
        Guid productId,
        Guid sourceWarehouseLocationId,
        decimal receivedQuantity,
        decimal previouslyPutawayQuantity,
        decimal outstandingQuantity,
        decimal putawayQuantity,
        Guid? unitOfMeasureId = null,
        string? batchNumber = null,
        string? serialNumber = null,
        DateTime? expiryDate = null,
        string? notes = null)
    {
        if (putawayQuantity <= 0)
            throw new ArgumentException("Putaway quantity must be greater than zero.", nameof(putawayQuantity));

        return new PutawayLine
        {
            PutawayLineId = Guid.NewGuid(),
            PutawayId = putawayId,
            SourceDocumentLineId = sourceDocumentLineId,
            ProductId = productId,
            SourceWarehouseLocationId = sourceWarehouseLocationId,
            ReceivedQuantity = Math.Round(receivedQuantity, 4, MidpointRounding.AwayFromZero),
            PreviouslyPutawayQuantity = Math.Round(previouslyPutawayQuantity, 4, MidpointRounding.AwayFromZero),
            OutstandingQuantity = Math.Round(outstandingQuantity, 4, MidpointRounding.AwayFromZero),
            PutawayQuantity = Math.Round(putawayQuantity, 4, MidpointRounding.AwayFromZero),
            UnitOfMeasureId = unitOfMeasureId,
            BatchNumber = batchNumber?.Trim(),
            SerialNumber = serialNumber?.Trim(),
            ExpiryDate = expiryDate,
            Notes = notes?.Trim()
        };
    }

    public void AddAllocation(PutawayLineAllocation allocation)
    {
        allocation.PutawayLineId = this.PutawayLineId;
        Allocations.Add(allocation);
    }
}
