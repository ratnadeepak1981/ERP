using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.CustomerCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CustomerName)
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

        builder.Property(x => x.CreditLimit)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.PaymentTerms)
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Tenant + CustomerCode must be unique
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerCode
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId
        })
        .IsUnique();
    }
}