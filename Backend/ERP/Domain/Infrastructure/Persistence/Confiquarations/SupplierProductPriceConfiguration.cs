using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class SupplierProductPriceConfiguration : IEntityTypeConfiguration<SupplierProductPrice>
{
    public void Configure(EntityTypeBuilder<SupplierProductPrice> builder)
    {
        builder.ToTable("SupplierProductPrices");

        builder.HasKey(x => x.SupplierProductPriceId);

        builder.Property(x => x.SupplierItemCode)
            .HasMaxLength(50);

        builder.Property(x => x.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(x => x.MinimumOrderQuantity)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.EffectiveFrom)
            .IsRequired();

        builder.Property(x => x.IsPreferred)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Indexes for lookups
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId,
            x.ProductId
        });

        // SupplierProductPrice → Supplier (Tenant-aware composite foreign key)
        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SupplierId })
            .HasPrincipalKey(x => new { x.TenantId, x.SupplierId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierProductPrice → Product (Tenant-aware composite foreign key)
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierProductPrice → UnitOfMeasure (Tenant-aware composite foreign key)
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .HasPrincipalKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
