using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class InventoryCostLayerConfiguration : IEntityTypeConfiguration<InventoryCostLayer>
{
    public void Configure(EntityTypeBuilder<InventoryCostLayer> builder)
    {
        builder.ToTable("InventoryCostLayers");

        builder.HasKey(x => x.CostLayerId);

        builder.Property(x => x.OriginalQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.RemainingQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.OriginalCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.RemainingCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SerialNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LayerDate)
            .IsRequired();

        builder.Property(x => x.IsExhausted)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // High-performance index for FIFO/LIFO layer consumption
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductId,
            x.WarehouseId,
            x.IsExhausted,
            x.LayerDate,
            x.CostLayerId
        });

        // Index for looking up layers by originating receipt transaction (e.g. for reversals)
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.InventoryTransactionId
        });

        // Foreign keys
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WarehouseLocation>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.WarehouseId, x.WarehouseLocationId })
            .HasPrincipalKey(x => new { x.TenantId, x.WarehouseId, x.WarehouseLocationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<InventoryTransaction>()
            .WithMany()
            .HasForeignKey(x => x.InventoryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
