using Domain.Features.Procurement.PurchaseOrder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.OrderDate)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Tenant + Company + OrderNumber unique
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.OrderNumber
        })
        .IsUnique();

        // Branch-level filter index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // 1-to-many relationship with items
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
