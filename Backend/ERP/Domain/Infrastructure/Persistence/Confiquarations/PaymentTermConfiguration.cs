using Domain.Features.MasterData.PaymentTerm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Confiquarations;

public class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> builder)
    {
        builder.ToTable("PaymentTerms");

        builder.HasKey(p => p.PaymentTermId);

        builder.Property(p => p.TermCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.TermName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Days)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(p => new { p.TenantId, p.TermCode })
            .IsUnique();

        builder.HasIndex(p => new { p.TenantId, p.PaymentTermId })
            .IsUnique();
    }
}
