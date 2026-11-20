using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.SalesQuotation;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesQuotationConfiguration : IEntityTypeConfiguration<SalesQuotation>
{
    public void Configure(EntityTypeBuilder<SalesQuotation> builder)
    {
        builder.ToTable("SalesQuotations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuotationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DocumentType)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.BaseCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ExchangeRateEffectiveDate).HasColumnType("date");
        builder.Property(x => x.AppliedExchangeRate).HasColumnType("decimal(28,12)");
        builder.Property(x => x.BaseSubTotal).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseDiscountAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseTaxAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseTotalAmount).HasColumnType("decimal(18,4)");

        builder.Property(x => x.PaymentTerms)
            .HasMaxLength(100);

        builder.Property(x => x.DeliveryTerms)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.QuotationDate)
            .IsRequired();

        builder.Property(x => x.ValidUntilDateUtc)
            .IsRequired();

        builder.Property(x => x.DeliveryDate)
            .IsRequired(false);

        builder.Property(x => x.SubTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.Property(x => x.CustomerRemarks)
            .HasMaxLength(2000);

        builder.Property(x => x.InternalNotes)
            .HasMaxLength(2000);

        builder.Property(x => x.RejectionRemarks)
            .HasMaxLength(1000);

        builder.Property(x => x.DeclinedRemarks)
            .HasMaxLength(1000);

        builder.Property(x => x.DeliveryAddressSnapshot)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.QuotationNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.BranchId });
        builder.HasIndex(x => new { x.TenantId, x.CustomerId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ValidUntilDateUtc });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(x => x.TransactionCurrencyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(x => x.BaseCurrencyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesQuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
