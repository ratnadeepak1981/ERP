using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.PurchaseRequisition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PurchaseRequisitionItemConfiguration : IEntityTypeConfiguration<PurchaseRequisitionItem>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisitionItem> builder)
    {
        builder.ToTable("PurchaseRequisitionItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.EstimatedUnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.EstimatedLineTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(250);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.UnitOfMeasureId)
            .IsRequired(false);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
