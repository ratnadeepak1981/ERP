using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("TenantDomains");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Hostname)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Subdomain)
            .IsRequired(false)
            .HasMaxLength(63);

        builder.Property(x => x.DomainType)
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.IsVerified)
            .IsRequired();

        // Hostname must be globally unique across all tenants
        builder.HasIndex(x => x.Hostname)
            .IsUnique();

        // Subdomain must be unique when specified
        builder.HasIndex(x => x.Subdomain)
            .IsUnique()
            .HasFilter("[Subdomain] IS NOT NULL");

        // Index on TenantId for tenant domain lookups
        builder.HasIndex(x => x.TenantId);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Domains)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
