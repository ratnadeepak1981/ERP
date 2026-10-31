using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class ProductIdentifierConfiguration : IEntityTypeConfiguration<ProductIdentifier>
{
    public void Configure(EntityTypeBuilder<ProductIdentifier> builder)
    {
        builder.ToTable("ProductIdentifiers");

        builder.HasKey(x => x.ProductIdentifierId);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.ProductId)
            .IsRequired();

        builder.Property(x => x.UnitOfMeasureId)
            .IsRequired(false);

        builder.Property(x => x.IdentifierValue)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IdentifierType)
            .IsRequired();

        builder.Property(x => x.Symbology)
            .IsRequired();

        builder.Property(x => x.Source)
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Foreign keys
        builder.HasOne<Product>()
            .WithMany(p => p.Identifiers)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Filtered Unique Index: Active identifier value must be unique per tenant
        builder.HasIndex(x => new { x.TenantId, x.IdentifierValue })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        // Filtered Unique Index: Only one active primary identifier per product and packaging UOM (when UOM is specified)
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.UnitOfMeasureId })
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1 AND [UnitOfMeasureId] IS NOT NULL");

        // Filtered Unique Index: Only one active primary item-level identifier per product (when UOM is null)
        builder.HasIndex(x => new { x.TenantId, x.ProductId })
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1 AND [UnitOfMeasureId] IS NULL");
    }
}
