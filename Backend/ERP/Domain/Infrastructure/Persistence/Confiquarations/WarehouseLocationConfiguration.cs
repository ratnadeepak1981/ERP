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

        builder.Property(x => x.WarehouseZoneId)
            .IsRequired(false);

        builder.Property(x => x.LocationTypeId)
            .IsRequired(false);

        builder.Property(x => x.ParentLocationId)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // LocationCode must be unique within a warehouse and tenant.
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.LocationCode
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseLocationId
        })
        .IsUnique();

        // Composite key for hierarchy foreign keys within tenant and warehouse
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.WarehouseLocationId
        })
        .IsUnique();

        // Location belongs to a Warehouse (Tenant-aware composite foreign key)
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);

        // Location belongs to an optional Zone
        builder.HasOne<WarehouseZone>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseZoneId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Parent location hierarchy (Enforces strictly identical TenantId and WarehouseId)
        builder.HasOne<WarehouseLocation>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId, x.ParentLocationId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId, x.WarehouseLocationId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // LocationType
        builder.HasOne<LocationType>()
            .WithMany()
            .HasForeignKey(x => x.LocationTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}