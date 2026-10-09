using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions", table =>
        {
            table.HasCheckConstraint("CK_PaymentTransactions_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_PaymentTransactions_AllocatedAmount_NonNegative", "[AllocatedAmount] >= 0");
            table.HasCheckConstraint("CK_PaymentTransactions_RefundedAmount_NonNegative", "[RefundedAmount] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.PaymentProvider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderTransactionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PaymentMethod)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.AllocatedAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.RefundedAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.ProcessedAtUtc)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency: Each tenant + provider + idempotency key must be unique
        builder.HasIndex(x => new { x.TenantId, x.PaymentProvider, x.IdempotencyKey })
            .IsUnique();

        // Provider transaction reference uniqueness
        builder.HasIndex(x => new { x.PaymentProvider, x.ProviderTransactionId })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}
