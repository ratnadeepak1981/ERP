using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class LocationIdentifierConfiguration : IEntityTypeConfiguration<LocationIdentifier>
{
    public void Configure(EntityTypeBuilder<LocationIdentifier> builder)
    {
        builder.ToTable("LocationIdentifiers");

        builder.HasKey(x => x.LocationIdentifierId);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.WarehouseId)
            .IsRequired();

        builder.Property(x => x.WarehouseLocationId)
            .IsRequired();

        builder.Property(x => x.IdentifierValue)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IdentifierType)
            .IsRequired();

        builder.Property(x => x.Symbology)
            .IsRequired();

        builder.Property(x => x.Source)
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Foreign keys
        builder.HasOne<WarehouseLocation>()
            .WithMany(l => l.Identifiers)
            .HasForeignKey(x => x.WarehouseLocationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered Unique Index: Active identifier value must be unique per tenant
        builder.HasIndex(x => new { x.TenantId, x.IdentifierValue })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        // Filtered Unique Index: Only one active primary identifier per warehouse location
        builder.HasIndex(x => new { x.TenantId, x.WarehouseLocationId })
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1");
    }
}
