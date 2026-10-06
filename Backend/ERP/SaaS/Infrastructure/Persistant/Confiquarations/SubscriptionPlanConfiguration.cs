using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration
    : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable(
            "SubscriptionPlans",
            table => table.HasCheckConstraint(
                "CK_SubscriptionPlans_Price_NonNegative",
                "[Price] >= 0"));

        builder.HasKey(x => x.Id);

        // Plan name
        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        // Stable plan code
        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        // Description
        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(500);

        // Price
        builder.Property(x => x.Price)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        // Billing cycle enum
        builder.Property(x => x.BillingCycle)
            .IsRequired()
            .HasConversion<int>();

        // Active status
        builder.Property(x => x.IsActive)
            .IsRequired();

        // SubscriptionPlan → SubscriptionLimit (1 : 1)
        builder.HasOne(x => x.SubscriptionLimit)
            .WithOne(x => x.SubscriptionPlan)
            .HasForeignKey<SubscriptionLimit>(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // SubscriptionPlan → Subscriptions (1 : many)
        builder.HasMany(x => x.Subscriptions)
            .WithOne(x => x.SubscriptionPlan)
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}