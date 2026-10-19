using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.ToTable("InventoryBalances");

        builder.HasKey(x => x.InventoryBalanceId);

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.QuantityOnHand)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.QuantityAllocated)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.QuantityQuarantine)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.QuantityOnOrder)
            .HasPrecision(18, 4)
            .IsRequired();

        // Optimistic concurrency token
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique Stock Identity Key
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseLocationId,
            x.ProductId,
            x.BatchNumber,
            x.SerialNumber
        })
        .IsUnique();

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
    }
}
