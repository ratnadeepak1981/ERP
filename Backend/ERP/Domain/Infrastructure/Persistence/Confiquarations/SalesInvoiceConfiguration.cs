using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> builder)
    {
        builder.ToTable("SalesInvoices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.InvoiceDate)
            .IsRequired();

        builder.Property(x => x.DueDate)
            .IsRequired(false);

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

        builder.Property(x => x.Subtotal)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique Invoice Number scoped to Tenant + Company
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.InvoiceNumber
        })
        .IsUnique();

        // Index for Sales Order lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SalesOrderId
        });

        // Index for Sales Shipment lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SalesShipmentId
        });

        // Branch-level filter index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // Customer index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId
        });

        // Status index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Status
        });

        // Foreign keys
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesShipment>()
            .WithMany()
            .HasForeignKey(x => x.SalesShipmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.TransactionCurrencyId })
            .HasPrincipalKey(x => new { x.TenantId, x.CurrencyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Currency>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.BaseCurrencyId })
            .HasPrincipalKey(x => new { x.TenantId, x.CurrencyId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ExchangeRate>()
            .WithMany()
            .HasForeignKey(x => x.ExchangeRateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1-to-many relationship with items
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
