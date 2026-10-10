using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class WarehouseConfiguration
    : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(x => x.WarehouseId);

        builder.Property(x => x.WarehouseCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.WarehouseName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.AddressLine1)
            .HasMaxLength(250);

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(250);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20);

        builder.Property(x => x.Country)
            .HasMaxLength(100);

        builder.Property(x => x.CompanyId)
            .IsRequired(false);

        builder.Property(x => x.BranchId)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // WarehouseCode is unique within a tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseCode
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.WarehouseId
        })
        .IsUnique();

        // Warehouse → Company (Tenant-aware composite foreign key)
        builder.HasOne<global::Domain.Features.MasterData.Company.Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Warehouse → Branch
        builder.HasOne<global::Domain.Features.MasterData.Branch.Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}