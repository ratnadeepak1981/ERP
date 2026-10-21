using Domain.Features.Inventory.Putaway;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PutawayConfiguration : IEntityTypeConfiguration<Putaway>
{
    public void Configure(EntityTypeBuilder<Putaway> builder)
    {
        builder.ToTable("Putaways");

        builder.HasKey(x => x.PutawayId);

        builder.Property(x => x.PutawayNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SourceDocumentNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.PutawayDate)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.SourceDocumentType)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique index: TenantId + CompanyId + PutawayNumber
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.PutawayNumber
        })
        .IsUnique();

        // Index for source receipt lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SourceDocumentId
        });

        // Index for warehouse lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId
        });

        // Index for status filtering
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Status
        });

        // 1-to-many relationship with PutawayLines
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PutawayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
