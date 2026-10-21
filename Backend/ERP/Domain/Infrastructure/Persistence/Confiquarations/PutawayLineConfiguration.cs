using Domain.Features.Inventory.Putaway;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PutawayLineConfiguration : IEntityTypeConfiguration<PutawayLine>
{
    public void Configure(EntityTypeBuilder<PutawayLine> builder)
    {
        builder.ToTable("PutawayLines");

        builder.HasKey(x => x.PutawayLineId);

        builder.Property(x => x.ReceivedQuantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.PreviouslyPutawayQuantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.OutstandingQuantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.PutawayQuantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100);

        builder.Property(x => x.SerialNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasIndex(x => x.PutawayId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.SourceDocumentLineId);

        builder.HasMany(x => x.Allocations)
            .WithOne()
            .HasForeignKey(x => x.PutawayLineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
