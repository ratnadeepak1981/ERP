using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionInvoiceConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
    {
        builder.ToTable("SubscriptionInvoices", table =>
        {
            table.HasCheckConstraint("CK_SubscriptionInvoices_SubTotal_NonNegative", "[SubTotal] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoices_TaxAmount_NonNegative", "[TaxAmount] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoices_TotalAmount_NonNegative", "[TotalAmount] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoices_PaidAmount_NonNegative", "[PaidAmount] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoices_CreditedAmount_NonNegative", "[CreditedAmount] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubscriptionId)
            .IsRequired();

        builder.Property(x => x.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SequenceNumber)
            .IsRequired();

        builder.Property(x => x.BillingCycle)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.BillingPeriodStart)
            .IsRequired();

        builder.Property(x => x.BillingPeriodEnd)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.SubTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TaxAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.PaidAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.CreditedAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.IssueDate)
            .IsRequired();

        builder.Property(x => x.DueDate)
            .IsRequired();

        builder.Property(x => x.PaidAt)
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Subscription)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InvoiceNumber)
            .IsUnique();

        // Prevent duplicate invoices covering identical billing period intervals for the subscription
        builder.HasIndex(x => new { x.SubscriptionId, x.BillingPeriodStart, x.BillingPeriodEnd })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => x.SubscriptionId);
    }
}
