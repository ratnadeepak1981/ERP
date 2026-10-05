using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class SupplierConfiguration
    : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(x => x.SupplierId);

        builder.Property(x => x.SupplierCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SupplierName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

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

        builder.Property(x => x.TaxNumber)
            .HasMaxLength(100);

        builder.Property(x => x.CreditTerms)
            .HasMaxLength(100);

        builder.Property(x => x.PaymentTerms)
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierCode
        })
        .IsUnique();
    }
}