using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanParameterConfiguration
    : IEntityTypeConfiguration<SubscriptionPlanParameter>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionPlanParameter> builder)
    {
        builder.ToTable(
            "SubscriptionPlanParameters",
            table =>
            {
                // Limit value cannot be negative.
                table.HasCheckConstraint(
                    "CK_SubscriptionPlanParameters_LimitValue_NonNegative",
                    "[LimitValue] IS NULL OR [LimitValue] >= 0");

                // Unlimited and LimitValue must be consistent.
                table.HasCheckConstraint(
                    "CK_SubscriptionPlanParameters_Unlimited_LimitValue",
                    "([IsUnlimited] = 1 AND [LimitValue] IS NULL) OR " +
                    "([IsUnlimited] = 0 AND [LimitValue] IS NOT NULL)");
            });

        // Primary Key
        builder.HasKey(x => x.Id);

        // Plan-specific limit value
        builder.Property(x => x.LimitValue)
            .HasColumnType("decimal(18,2)");

        // Unlimited flag
        builder.Property(x => x.IsUnlimited)
            .IsRequired();

        // Duration
        // null = Lifetime
        builder.Property(x => x.Duration)
            .HasConversion<int>()
            .IsRequired(false);

        // Active status
        builder.Property(x => x.IsActive)
            .IsRequired();

        // SubscriptionLimit → SubscriptionPlanParameters
        builder.HasOne(x => x.SubscriptionLimit)
            .WithMany(x => x.Parameters)
            .HasForeignKey(x => x.SubscriptionLimitId)
            .OnDelete(DeleteBehavior.Restrict);

        // SubscriptionParameter → SubscriptionPlanParameters
        builder.HasOne(x => x.SubscriptionParameter)
            .WithMany(x => x.PlanParameters)
            .HasForeignKey(x => x.SubscriptionParameterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Fast lookup by limit
        builder.HasIndex(x => x.SubscriptionLimitId);

        // Fast lookup by parameter
        builder.HasIndex(x => x.SubscriptionParameterId);

        // A parameter can occur only once within a plan's limit configuration
        builder.HasIndex(x => new
        {
            x.SubscriptionLimitId,
            x.SubscriptionParameterId
        })
        .IsUnique();
    }
}