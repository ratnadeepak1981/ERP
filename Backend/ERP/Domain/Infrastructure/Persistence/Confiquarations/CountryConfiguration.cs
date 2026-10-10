using Domain.Features.MasterData.Country;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("Countries");

        builder.HasKey(x => x.CountryId);

        builder.Property(x => x.Alpha2Code)
            .IsRequired()
            .HasMaxLength(2)
            .IsFixedLength();

        builder.Property(x => x.Alpha3Code)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(x => x.NumericCode)
            .HasMaxLength(3);

        builder.Property(x => x.CountryName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.OfficialName)
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => x.Alpha2Code)
            .IsUnique();

        builder.HasIndex(x => x.Alpha3Code)
            .IsUnique();

        builder.HasIndex(x => x.CountryName)
            .IsUnique();
    }
}
