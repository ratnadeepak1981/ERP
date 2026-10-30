using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class ProductPackagingConfiguration : IEntityTypeConfiguration<ProductPackaging>
{
    public void Configure(EntityTypeBuilder<ProductPackaging> builder)
    {
        builder.ToTable("ProductPackagings");

        builder.HasKey(x => x.ProductPackagingId);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CompanyId)
            .IsRequired(false);

        builder.Property(x => x.ProductId)
            .IsRequired();

        builder.Property(x => x.PackagingCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PackagingName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PackagingUomId)
            .IsRequired();

        builder.Property(x => x.BaseQuantity)
            .IsRequired()
            .HasColumnType("decimal(18,6)");

        builder.Property(x => x.PurchaseAllowed)
            .IsRequired();

        builder.Property(x => x.SalesAllowed)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Foreign keys
        builder.HasOne<Product>()
            .WithMany(p => p.Packagings)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => x.PackagingUomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Unique index: Tenant-scoped PackagingCode must be unique
        builder.HasIndex(x => new { x.TenantId, x.PackagingCode })
            .IsUnique();

        // Lookup index: Fast resolution by Product and Packaging UOM within Tenant
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.PackagingUomId });

        // Optional company index
        builder.HasIndex(x => new { x.TenantId, x.CompanyId });
    }
}
