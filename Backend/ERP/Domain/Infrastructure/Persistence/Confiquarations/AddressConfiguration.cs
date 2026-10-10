using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Country;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.HasKey(x => x.AddressId);

        builder.Property(x => x.AddressName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AddressLine1)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(250);

        builder.Property(x => x.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StateProvince)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20);

        builder.Property(x => x.UnmatchedCountryText)
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AddressId
        })
        .IsUnique();

        // Address → Country (Shared reference lookup)
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
