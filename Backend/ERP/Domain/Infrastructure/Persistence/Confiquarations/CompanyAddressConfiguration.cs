using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using Domain.Features.MasterData.Company;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CompanyAddressConfiguration : IEntityTypeConfiguration<CompanyAddress>
{
    public void Configure(EntityTypeBuilder<CompanyAddress> builder)
    {
        builder.ToTable("CompanyAddresses");

        builder.HasKey(x => x.CompanyAddressId);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact address and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.AddressId,
            x.AddressTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active default address per address type for a company
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.AddressTypeId
        })
        .IsUnique()
        .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");

        // CompanyAddress → Company (Tenant-aware composite foreign key)
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // CompanyAddress → Address (Tenant-aware composite foreign key)
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AddressId })
            .HasPrincipalKey(x => new { x.TenantId, x.AddressId })
            .OnDelete(DeleteBehavior.Restrict);

        // CompanyAddress → AddressType
        builder.HasOne<AddressType>()
            .WithMany()
            .HasForeignKey(x => x.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
