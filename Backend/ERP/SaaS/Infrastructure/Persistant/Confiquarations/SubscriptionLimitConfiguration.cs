using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionLimitConfiguration
    : IEntityTypeConfiguration<SubscriptionLimit>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionLimit> builder)
    {
        builder.ToTable("SubscriptionLimits");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Active status
        builder.Property(x => x.IsActive)
            .IsRequired();

        // SubscriptionPlan → SubscriptionLimit (1 : 1)
        builder.HasOne(x => x.SubscriptionPlan)
            .WithOne(x => x.SubscriptionLimit)
            .HasForeignKey<SubscriptionLimit>(
                x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // One SubscriptionLimit per SubscriptionPlan
        builder.HasIndex(x => x.SubscriptionPlanId)
            .IsUnique();
    }
}