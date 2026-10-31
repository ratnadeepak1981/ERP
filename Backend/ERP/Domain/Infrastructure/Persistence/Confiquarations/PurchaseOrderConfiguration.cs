using Domain.Features.MasterData.PaymentTerm;
using Domain.Features.MasterData.Currency;
using Domain.Features.Procurement.PurchaseOrder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

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
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("USD");

        builder.Property(x => x.BaseCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ExchangeRateEffectiveDate).HasColumnType("date");
        builder.Property(x => x.AppliedExchangeRate).HasColumnType("decimal(28,12)");
        builder.Property(x => x.BaseSubTotal).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseDiscountAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseTaxAmount).HasColumnType("decimal(18,4)");
        builder.Property(x => x.BaseTotalAmount).HasColumnType("decimal(18,4)");

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
            .HasForeignKey(x => new { x.TenantId, x.ExchangeRateId })
            .HasPrincipalKey(x => new { x.TenantId, x.ExchangeRateId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.OrderDate)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.SupplierId)
            .IsRequired(false);

        builder.Property(x => x.CreatedByUserId)
            .IsRequired(false);

        // Payment Terms FK (nullable – populated from supplier default or request override)
        builder.Property(x => x.PaymentTermId)
            .IsRequired(false);

        builder.Property(x => x.DueDate)
            .IsRequired(false);

        builder.Property(x => x.ExpectedDeliveryDate)
            .IsRequired(false);

        // Tenant + Company + OrderNumber unique
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

        // Supplier index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId
        });

        builder.HasOne<ERP.Domain.Features.MasterData.Supplier.Supplier>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PaymentTerm>()
            .WithMany()
            .HasForeignKey(x => x.PaymentTermId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1-to-many relationship with items
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
