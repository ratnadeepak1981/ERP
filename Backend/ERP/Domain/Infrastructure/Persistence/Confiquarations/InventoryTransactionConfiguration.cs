using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Domain.Infrastructure.Persistence.Confiquarations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");

        builder.HasKey(x => x.InventoryTransactionId);

        builder.Property(x => x.TransactionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.MovementType)
            .IsRequired();

        builder.Property(x => x.SourceDocumentType)
            .IsRequired();

        builder.Property(x => x.SourceDocumentId)
            .IsRequired();

        builder.Property(x => x.SourceDocumentLineId)
            .IsRequired();

        builder.Property(x => x.MovementSequence)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.SerialNumber)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.TransactionDate)
            .IsRequired();

        builder.Property(x => x.ReversalOfTransactionId)
            .IsRequired(false);

        // Unique transaction number per tenant
        builder.HasIndex(x => new { x.TenantId, x.TransactionNumber })
            .IsUnique();

        // Idempotency Key: Prevents duplicate postings of the same source document line sequence
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SourceDocumentType,
            x.SourceDocumentId,
            x.SourceDocumentLineId,
            x.MovementType,
            x.MovementSequence
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

        builder.HasOne<InventoryTransaction>()
            .WithMany()
            .HasForeignKey(x => x.ReversalOfTransactionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
