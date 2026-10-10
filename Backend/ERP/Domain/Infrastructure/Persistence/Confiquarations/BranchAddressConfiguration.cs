using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.AddressType;
using Domain.Features.MasterData.Branch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class BranchAddressConfiguration : IEntityTypeConfiguration<BranchAddress>
{
    public void Configure(EntityTypeBuilder<BranchAddress> builder)
    {
        builder.ToTable("BranchAddresses");

        builder.HasKey(x => x.BranchAddressId);

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact address and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.BranchId,
            x.AddressId,
            x.AddressTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active default address per address type for a branch
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.BranchId,
            x.AddressTypeId
        })
        .IsUnique()
        .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");

        // BranchAddress → Branch
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // BranchAddress → Address (Tenant-aware composite foreign key)
        builder.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.AddressId })
            .HasPrincipalKey(x => new { x.TenantId, x.AddressId })
            .OnDelete(DeleteBehavior.Restrict);

        // BranchAddress → AddressType
        builder.HasOne<AddressType>()
            .WithMany()
            .HasForeignKey(x => x.AddressTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
