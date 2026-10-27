using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory.Batch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class ProductBatchConfiguration : IEntityTypeConfiguration<ProductBatch>
{
    public void Configure(EntityTypeBuilder<ProductBatch> builder)
    {
        builder.ToTable("ProductBatches");

        builder.HasKey(x => x.ProductBatchId);

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique index: a batch number belongs to a specific product within a tenant
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.BatchNumber })
            .IsUnique();

        // Secondary index on TenantId and BatchNumber for fast tenant-wide uniqueness / mismatch validation
        builder.HasIndex(x => new { x.TenantId, x.BatchNumber });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ProductId })
            .HasPrincipalKey(x => new { x.TenantId, x.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
