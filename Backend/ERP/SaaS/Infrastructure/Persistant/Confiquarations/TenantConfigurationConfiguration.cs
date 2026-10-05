using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class TenantConfigurationConfiguration
    : IEntityTypeConfiguration<TenantConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantConfiguration> builder)
    {
        builder.ToTable("TenantConfigurations");

        builder.HasKey(x => x.TenantConfigurationId);

        builder.Property(x => x.TenantConfigurationId)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CompanyEnabled)
            .IsRequired();

        builder.Property(x => x.BranchEnabled)
            .IsRequired();

        builder.Property(x => x.WarehouseZoneEnabled)
            .IsRequired();

        builder.Property(x => x.WarehouseBinEnabled)
            .IsRequired();

        // One configuration per tenant
        builder.HasIndex(x => x.TenantId)
            .IsUnique();

        // Tenant → TenantConfiguration
        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<TenantConfiguration>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}