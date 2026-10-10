using Domain.Features.MasterData.Product;
using ERP.Domain.Features.MasterData.Category;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class ProductConfiguration
    : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(x => x.ProductId);

        builder.Property(x => x.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ValuationMethod)
            .IsRequired();

        builder.Property(x => x.StandardCost)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.SellingPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.ReorderLevel)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.ReorderQuantity)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.IsManufacturable)
            .IsRequired();

        builder.Property(x => x.IsPurchasable)
            .IsRequired();

        builder.Property(x => x.IsSellable)
            .IsRequired();

        builder.Property(x => x.CanConsumeInProduction)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CanConsumeInMaintenance)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsStockTracked)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.ProductType)
            .IsRequired()
            .HasDefaultValue(ProductType.StockItem);

        builder.Property(x => x.TrackingMode)
            .IsRequired()
            .HasDefaultValue(TrackingMode.None);

        builder.Property(x => x.UnitOfMeasureId)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Duplicate constraints
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductName
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductCode
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductId
        })
        .IsUnique();

        // Product → Category (Tenant-aware composite foreign key)
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.CategoryId })
            .OnDelete(DeleteBehavior.Restrict);

        // Product → UnitOfMeasure (Tenant-aware composite foreign key)
        builder.HasOne<global::Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .HasPrincipalKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}