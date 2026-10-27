using Domain.Features.MasterData.Product;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class ProductWarehouseSettingConfiguration : IEntityTypeConfiguration<ProductWarehouseSetting>
{
    public void Configure(EntityTypeBuilder<ProductWarehouseSetting> builder)
    {
        builder.ToTable("ProductWarehouseSettings");

        builder.HasKey(x => x.ProductWarehouseSettingId);

        builder.Property(x => x.ReorderLevel)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.ReorderQuantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.MinStockQuantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.MaxStockQuantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.SafetyStockQuantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.LeadTimeDays)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique composite index: exactly one setting record per (Tenant, Product, Warehouse)
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductId,
            x.WarehouseId
        })
        .IsUnique();

        // Secondary index for warehouse lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId
        });

        // Secondary index for product lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductId
        });

        // Foreign Key: ProductWarehouseSetting -> Product (Tenant-aware composite FK)
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign Key: ProductWarehouseSetting -> Warehouse (Tenant-aware composite FK)
        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
