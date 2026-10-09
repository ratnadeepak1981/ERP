using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("RefundTransactions", table =>
        {
            table.HasCheckConstraint("CK_RefundTransactions_Amount_Positive", "[Amount] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.PaymentTransactionId)
            .IsRequired();

        builder.Property(x => x.ProviderRefundId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.ProcessedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany(x => x.Refunds)
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProviderRefundId)
            .IsUnique();

        builder.HasIndex(x => x.PaymentTransactionId);
        builder.HasIndex(x => x.TenantId);
    }
}
