using Domain.Features.Inventory.Putaway;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PutawayLineAllocationConfiguration : IEntityTypeConfiguration<PutawayLineAllocation>
{
    public void Configure(EntityTypeBuilder<PutawayLineAllocation> builder)
    {
        builder.ToTable("PutawayLineAllocations");

        builder.HasKey(x => x.PutawayLineAllocationId);

        builder.Property(x => x.AllocatedQuantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.HasIndex(x => x.PutawayLineId);
        builder.HasIndex(x => x.DestinationWarehouseLocationId);
        builder.HasIndex(x => x.InventoryTransactionId);
    }
}
