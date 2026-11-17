using Domain.Features.Procurement.RequestForQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SupplierQuotationConfiguration : IEntityTypeConfiguration<SupplierQuotation>
{
    public void Configure(EntityTypeBuilder<SupplierQuotation> builder)
    {
        builder.ToTable("SupplierQuotations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SupplierName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.QuotationReference)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.QuotationDate)
            .IsRequired();

        builder.Property(x => x.ValidUntil)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.ExchangeRate)
            .HasColumnType("decimal(28,12)")
            .HasDefaultValue(1.0m)
            .IsRequired();

        builder.Property(x => x.SubTotal)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.BaseTotalAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(x => x.ApprovalRemarks)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.RejectionRemarks)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.RFQId, x.Status })
            .HasDatabaseName("IX_SupplierQuotations_Tenant_RFQ_Status");

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.SupplierQuotation)
            .HasForeignKey(x => x.SupplierQuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
