using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class WarehouseAddressConfiguration : IEntityTypeConfiguration<WarehouseAddress>
{
    public void Configure(EntityTypeBuilder<WarehouseAddress> builder)
    {
        builder.ToTable("WarehouseAddresses");

        builder.HasKey(x => x.WarehouseAddressId);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact address and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.AddressId,
            x.AddressTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active default address per address type for a warehouse
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId,
            x.AddressTypeId
        })
        .IsUnique()
        .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");

        // WarehouseAddress → Warehouse (Tenant-aware composite foreign key)
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);

        // WarehouseAddress → Address (Tenant-aware composite foreign key)
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AddressId })
            .HasPrincipalKey(x => new { x.TenantId, x.AddressId })
            .OnDelete(DeleteBehavior.Restrict);

        // WarehouseAddress → AddressType
        builder.HasOne<AddressType>()
            .WithMany()
            .HasForeignKey(x => x.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
