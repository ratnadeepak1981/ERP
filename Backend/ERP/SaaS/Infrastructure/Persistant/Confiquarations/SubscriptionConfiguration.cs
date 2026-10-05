using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration
    : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SubscriptionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StorageMode)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Tenant → Subscriptions
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Fast tenant lookup
        builder.HasIndex(x => x.TenantId);

        // Prevent duplicate subscription names within a tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SubscriptionName
        })
        .IsUnique();
    }
}