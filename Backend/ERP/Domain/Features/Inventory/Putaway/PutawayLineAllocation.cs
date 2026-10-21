using System;
using ERP.Domain.Common;

namespace Domain.Features.Inventory.Putaway;

public class PutawayLineAllocation : Auditable
{
    public Guid PutawayLineAllocationId { get; set; }
    public Guid PutawayLineId { get; set; }

    public Guid DestinationWarehouseLocationId { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public int MovementSequence { get; set; } = 1;

    public Guid? InventoryTransactionId { get; set; }

    public static PutawayLineAllocation Create(
        Guid putawayLineId,
        Guid destinationWarehouseLocationId,
        decimal allocatedQuantity,
        int movementSequence = 1)
    {
        if (allocatedQuantity <= 0)
            throw new ArgumentException("Allocated quantity must be greater than zero.", nameof(allocatedQuantity));

        return new PutawayLineAllocation
        {
            PutawayLineAllocationId = Guid.NewGuid(),
            PutawayLineId = putawayLineId,
            DestinationWarehouseLocationId = destinationWarehouseLocationId,
            AllocatedQuantity = Math.Round(allocatedQuantity, 4, MidpointRounding.AwayFromZero),
            MovementSequence = movementSequence
        };
    }
}
