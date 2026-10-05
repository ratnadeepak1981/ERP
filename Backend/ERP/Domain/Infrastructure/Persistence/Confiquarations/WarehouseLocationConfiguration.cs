using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class WarehouseLocationConfiguration
    : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ToTable("WarehouseLocations");

        builder.HasKey(x => x.WarehouseLocationId);

        builder.Property(x => x.LocationCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.LocationName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // LocationCode must be unique within a zone,
        // warehouse and tenant.
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.WarehouseZoneId,
            x.LocationCode
        })
        .IsUnique();

        // Location belongs to a Warehouse.
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Location belongs to a Zone.
        builder.HasOne<WarehouseZone>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseZoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}