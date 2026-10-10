using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");

        builder.HasKey(x => x.CustomerAddressId);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact address and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId,
            x.AddressId,
            x.AddressTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active default address per address type for a customer
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId,
            x.AddressTypeId
        })
        .IsUnique()
        .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");

        // CustomerAddress → Customer (Tenant-aware composite foreign key)
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerAddress → Address (Tenant-aware composite foreign key)
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AddressId })
            .HasPrincipalKey(x => new { x.TenantId, x.AddressId })
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerAddress → AddressType
        builder.HasOne<AddressType>()
            .WithMany()
            .HasForeignKey(x => x.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
