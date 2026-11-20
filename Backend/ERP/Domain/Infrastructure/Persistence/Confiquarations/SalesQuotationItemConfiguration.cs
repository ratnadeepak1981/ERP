using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.TaxCode;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.SalesQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesQuotationItemConfiguration : IEntityTypeConfiguration<SalesQuotationItem>
{
    public void Configure(EntityTypeBuilder<SalesQuotationItem> builder)
    {
        builder.ToTable("SalesQuotationItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.DiscountRate)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.TaxCodeId)
            .IsRequired(false);

        builder.Property(x => x.TaxRate)
            .HasColumnType("decimal(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.IsTaxInclusive)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.SubTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.LineTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.SalesQuotationId);
        builder.HasIndex(x => x.ProductId);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<TaxCode>()
            .WithMany()
            .HasForeignKey(x => x.TaxCodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
