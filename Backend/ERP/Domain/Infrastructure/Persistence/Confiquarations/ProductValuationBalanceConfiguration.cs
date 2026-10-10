using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class ProductValuationBalanceConfiguration : IEntityTypeConfiguration<ProductValuationBalance>
{
    public void Configure(EntityTypeBuilder<ProductValuationBalance> builder)
    {
        builder.ToTable("ProductValuationBalances");

        builder.HasKey(x => x.ProductValuationBalanceId);

        builder.Property(x => x.TotalQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalCostValue)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CurrentAverageCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique valuation balance per Tenant, Product, and Warehouse
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ProductId,
            x.WarehouseId
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
    }
}
