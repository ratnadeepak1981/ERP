using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.Inventory.StockCount;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public sealed class StockCountConfiguration : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> builder)
    {
        builder.ToTable("StockCounts");
        builder.HasKey(x => x.StockCountId);
        builder.HasAlternateKey(x => new { x.TenantId, x.StockCountId, x.WarehouseLocationId });

        builder.Property(x => x.CountNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.SnapshotFingerprint).HasMaxLength(128);
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.CountNumber }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
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

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => new { x.TenantId, x.StockCountId, x.WarehouseLocationId })
            .HasPrincipalKey(x => new { x.TenantId, x.StockCountId, x.WarehouseLocationId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockCountLineConfiguration : IEntityTypeConfiguration<StockCountLine>
{
    public void Configure(EntityTypeBuilder<StockCountLine> builder)
    {
        builder.ToTable("StockCountLines");
        builder.HasKey(x => x.StockCountLineId);

        builder.Property(x => x.ProductCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UnitOfMeasureCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ExpectedQuantityBase).HasPrecision(18, 4);
        builder.Property(x => x.PhysicalQuantityBase).HasPrecision(18, 4);
        builder.Property(x => x.VarianceQuantityBase).HasPrecision(18, 4);
        builder.Property(x => x.ExpectedQuantityEntered).HasPrecision(18, 4);
        builder.Property(x => x.PhysicalQuantityEntered).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.BatchNumber).HasMaxLength(100);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Remarks).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.StockCountId });
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.WarehouseLocationId });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .HasPrincipalKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.Restrict);

    }
}
