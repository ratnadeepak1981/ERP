using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations", table =>
        {
            table.HasCheckConstraint("CK_PaymentAllocations_Amount_Positive", "[Amount] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.PaymentTransactionId)
            .IsRequired();

        builder.Property(x => x.SubscriptionInvoiceId)
            .IsRequired();

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.AllocatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany(x => x.Allocations)
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubscriptionInvoice)
            .WithMany(x => x.PaymentAllocations)
            .HasForeignKey(x => x.SubscriptionInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PaymentTransactionId);
        builder.HasIndex(x => x.SubscriptionInvoiceId);
        builder.HasIndex(x => x.TenantId);
    }
}
