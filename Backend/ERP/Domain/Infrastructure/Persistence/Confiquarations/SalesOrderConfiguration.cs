using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.Sales.SalesOrder;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
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

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.OrderDate)
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
            .HasMaxLength(1000);

        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired(false);

        builder.Property(x => x.OpportunityId)
            .IsRequired(false);

        builder.Property(x => x.DeliveryAddressId)
            .IsRequired(false);

        builder.Property(x => x.DeliveryAddressSnapshot)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Ignore(x => x.PriceCardId);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique Order Number scoped to Tenant + Company
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.OrderNumber
        })
        .IsUnique();

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

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
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

        // 1-to-many relationship with items
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
