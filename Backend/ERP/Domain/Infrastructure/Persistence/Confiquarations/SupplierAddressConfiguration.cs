using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class SupplierAddressConfiguration : IEntityTypeConfiguration<SupplierAddress>
{
    public void Configure(EntityTypeBuilder<SupplierAddress> builder)
    {
        builder.ToTable("SupplierAddresses");

        builder.HasKey(x => x.SupplierAddressId);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact address and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId,
            x.AddressId,
            x.AddressTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active default address per address type for a supplier
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId,
            x.AddressTypeId
        })
        .IsUnique()
        .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");

        // SupplierAddress → Supplier (Tenant-aware composite foreign key)
        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SupplierId })
            .HasPrincipalKey(x => new { x.TenantId, x.SupplierId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierAddress → Address (Tenant-aware composite foreign key)
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AddressId })
            .HasPrincipalKey(x => new { x.TenantId, x.AddressId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierAddress → AddressType
        builder.HasOne<AddressType>()
            .WithMany()
            .HasForeignKey(x => x.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
