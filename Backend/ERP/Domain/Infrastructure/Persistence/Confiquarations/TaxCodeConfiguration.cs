using Domain.Features.MasterData.TaxCode;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Confiquarations;

public class TaxCodeConfiguration : IEntityTypeConfiguration<TaxCode>
{
    public void Configure(EntityTypeBuilder<TaxCode> builder)
    {
        builder.ToTable("TaxCodes");

        builder.HasKey(t => t.TaxCodeId);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Rate)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(t => t.Applicability)
            .IsRequired();

        builder.Property(t => t.EffectiveFrom)
            .IsRequired(false);

        builder.Property(t => t.EffectiveTo)
            .IsRequired(false);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(t => new { t.TenantId, t.Code })
            .IsUnique();

        builder.HasIndex(t => new { t.TenantId, t.TaxCodeId })
            .IsUnique();
    }
}
