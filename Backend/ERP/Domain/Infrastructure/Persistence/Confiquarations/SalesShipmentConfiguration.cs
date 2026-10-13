using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class SalesShipmentConfiguration : IEntityTypeConfiguration<SalesShipment>
{
    public void Configure(EntityTypeBuilder<SalesShipment> builder)
    {
        builder.ToTable("SalesShipments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShipmentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Method)
            .IsRequired();

        builder.Property(x => x.ShipmentDate)
            .IsRequired();

        builder.Property(x => x.DispatchedAt)
            .IsRequired(false);

        builder.Property(x => x.DeliveredAt)
            .IsRequired(false);

        builder.Property(x => x.CarrierName)
            .HasMaxLength(100);

        builder.Property(x => x.TrackingNumber)
            .HasMaxLength(100);

        builder.Property(x => x.RecipientName)
            .HasMaxLength(100);

        builder.Property(x => x.DeliveryAddress)
            .HasMaxLength(500);

        builder.Property(x => x.VehicleNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.WarehouseId)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique Shipment Number scoped to Tenant + Company
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.ShipmentNumber
        })
        .IsUnique();

        // Index for Sales Order lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SalesOrderId
        });

        // Branch-level filter index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CompanyId,
            x.BranchId
        });

        // Customer index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId
        });

        // Status index
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Status
        });

        // Foreign keys
        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CompanyId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1-to-many relationship with items
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
