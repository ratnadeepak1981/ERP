using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class GoodsReceiptNoteLineConfiguration : IEntityTypeConfiguration<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNoteLine>
{
    public void Configure(EntityTypeBuilder<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNoteLine> builder)
    {
        builder.ToTable("GoodsReceiptNoteLines");

        builder.HasKey(x => x.GoodsReceiptNoteLineId);

        builder.Property(x => x.QuantityReceived)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.LineTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100);

        builder.Property(x => x.SerialNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.IsQuarantine)
            .IsRequired();

        // Foreign key to PurchaseOrderItem
        builder.HasOne<Domain.Features.Procurement.PurchaseOrder.PurchaseOrderItem>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to Product
        builder.HasOne<Domain.Features.MasterData.Product.Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to Warehouse
        builder.HasOne<ERP.Domain.Features.MasterData.Warehouse.Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to WarehouseLocation
        builder.HasOne<ERP.Domain.Features.MasterData.Warehouse.WarehouseLocation>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
