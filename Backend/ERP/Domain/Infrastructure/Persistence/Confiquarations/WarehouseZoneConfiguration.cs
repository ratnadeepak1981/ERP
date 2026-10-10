using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class WarehouseZoneConfiguration
    : IEntityTypeConfiguration<WarehouseZone>
{
    public void Configure(EntityTypeBuilder<WarehouseZone> builder)
    {
        builder.ToTable("WarehouseZones");

        builder.HasKey(x => x.WarehouseZoneId);

        builder.Property(x => x.ZoneCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ZoneName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // ZoneCode must be unique within a warehouse and tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.ZoneCode
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseZoneId
        })
        .IsUnique();

        // Composite key including warehouse for location hierarchy foreign key
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.WarehouseZoneId
        })
        .IsUnique();

        // WarehouseZone → Warehouse (Tenant-aware composite foreign key)
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}