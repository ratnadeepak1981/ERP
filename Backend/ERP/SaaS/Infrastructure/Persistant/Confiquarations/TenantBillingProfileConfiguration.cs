using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class TenantBillingProfileConfiguration : IEntityTypeConfiguration<TenantBillingProfile>
{
    public void Configure(EntityTypeBuilder<TenantBillingProfile> builder)
    {
        builder.ToTable("TenantBillingProfiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.BillingEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TaxId)
            .HasMaxLength(50);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.AddressLine1)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(250);

        builder.Property(x => x.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StateOrProvince)
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.CountryCode)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(x => x.IsTaxExempt)
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenantId)
            .IsUnique();
    }
}
