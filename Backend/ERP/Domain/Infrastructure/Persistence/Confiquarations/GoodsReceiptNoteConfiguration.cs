using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class GoodsReceiptNoteConfiguration : IEntityTypeConfiguration<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNote>
{
    public void Configure(EntityTypeBuilder<Domain.Features.Procurement.GoodsReceiptNote.GoodsReceiptNote> builder)
    {
        builder.ToTable("GoodsReceiptNotes");

        builder.HasKey(x => x.GoodsReceiptNoteId);

        builder.Property(x => x.GrnNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DeliveryNoteNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.ReceiptDate)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique index: TenantId + CompanyId + GrnNumber
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.GrnNumber
        })
        .IsUnique();

        // Index for PO lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PurchaseOrderId
        });

        // Index for Branch lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // Relationship with Lines
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.GoodsReceiptNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign Key to PurchaseOrder
        builder.HasOne<Domain.Features.Procurement.PurchaseOrder.PurchaseOrder>()
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
