using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class TenantEntityConfiguration
    : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasMany<Subscription>()
            .WithOne(x => x.Tenant)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}