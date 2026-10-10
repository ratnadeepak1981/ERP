using Domain.Features.Procurement.PurchaseRequisition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PurchaseRequisitionConfiguration : IEntityTypeConfiguration<PurchaseRequisition>
{
    public void Configure(EntityTypeBuilder<PurchaseRequisition> builder)
    {
        builder.ToTable("PurchaseRequisitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequisitionNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.RequisitionDate)
            .IsRequired();

        builder.Property(x => x.RequiredDate)
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique per tenant + company + requisition number
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.RequisitionNumber
        })
        .IsUnique();

        // Branch-level filter index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PurchaseRequisitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
