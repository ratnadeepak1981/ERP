using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionInvoiceLineConfiguration : IEntityTypeConfiguration<SubscriptionInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SubscriptionInvoiceLine> builder)
    {
        builder.ToTable("SubscriptionInvoiceLines", table =>
        {
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_UnitPrice_NonNegative", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_SubTotal_NonNegative", "[SubTotal] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_TaxRate_NonNegative", "[TaxRate] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_TaxAmount_NonNegative", "[TaxAmount] >= 0");
            table.HasCheckConstraint("CK_SubscriptionInvoiceLines_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubscriptionInvoiceId)
            .IsRequired();

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.SubTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TaxRate)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.TaxAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TotalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubscriptionInvoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.SubscriptionInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubscriptionInvoiceId);
        builder.HasIndex(x => x.TenantId);
    }
}
