using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class TenantDatabaseConfiguration
    : IEntityTypeConfiguration<TenantDatabase>
{
    public void Configure(
        EntityTypeBuilder<TenantDatabase> builder)
    {
        builder.ToTable("TenantDatabases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DatabaseServer)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DatabaseName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => x.TenantId)
            .IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithOne()
            .HasForeignKey<TenantDatabase>(
                x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}