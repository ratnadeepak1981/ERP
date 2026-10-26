using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.Invoice;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesInvoiceItemConfiguration : IEntityTypeConfiguration<SalesInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceItem> builder)
    {
        builder.ToTable("SalesInvoiceItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.DiscountRate)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.TaxCodeId)
            .IsRequired(false);

        builder.Property(x => x.TaxRate)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.Subtotal)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.LineTotal)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.IsTaxInclusive)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Foreign keys
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesOrderItem>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesShipmentItem>()
            .WithMany()
            .HasForeignKey(x => x.SalesShipmentItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
