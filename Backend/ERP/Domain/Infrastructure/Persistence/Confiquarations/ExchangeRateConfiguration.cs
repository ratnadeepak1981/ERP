using Domain.Features.MasterData.Currency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Confiquarations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.HasKey(rate => rate.ExchangeRateId);

        builder.Property(rate => rate.RateType).IsRequired();
        builder.Property(rate => rate.EffectiveDate).HasColumnType("date").IsRequired();
        builder.Property(rate => rate.Rate).HasColumnType("decimal(28,12)").IsRequired();
        builder.Property(rate => rate.IsActive).HasDefaultValue(true).IsRequired();

        builder.HasIndex(rate => new
        {
            rate.TenantId,
            rate.FromCurrencyId,
            rate.ToCurrencyId,
            rate.RateType,
            rate.EffectiveDate
        }).IsUnique();

        builder.HasIndex(rate => new
        {
            rate.TenantId,
            rate.FromCurrencyId,
            rate.ToCurrencyId,
            rate.RateType,
            rate.IsActive,
            rate.EffectiveDate
        });

        builder.HasAlternateKey(rate => new { rate.TenantId, rate.ExchangeRateId });

        builder.HasOne(rate => rate.FromCurrency)
            .WithMany()
            .HasForeignKey(rate => new { rate.TenantId, rate.FromCurrencyId })
            .HasPrincipalKey(currency => new { currency.TenantId, currency.CurrencyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rate => rate.ToCurrency)
            .WithMany()
            .HasForeignKey(rate => new { rate.TenantId, rate.ToCurrencyId })
            .HasPrincipalKey(currency => new { currency.TenantId, currency.CurrencyId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
