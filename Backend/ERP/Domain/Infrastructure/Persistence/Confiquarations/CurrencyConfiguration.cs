using Domain.Features.MasterData.Currency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.ToTable("Currencies");

        builder.HasKey(x => x.CurrencyId);

        builder.Property(x => x.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.CurrencyName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.DecimalPlaces)
            .IsRequired()
            .HasDefaultValue(2);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CurrencyCode
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CurrencyId
        })
        .IsUnique();
    }
}
