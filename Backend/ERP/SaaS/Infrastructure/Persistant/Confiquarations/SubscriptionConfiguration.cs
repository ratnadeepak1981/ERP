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

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubscriptionPlanId)
            .IsRequired();

        builder.Property(x => x.SubscriptionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SubscriptionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StorageMode)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.BillingCycle)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.BillingPlanPriceSnapshot)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.BillingAnchorDate)
            .IsRequired();

        builder.Property(x => x.CurrentPeriodStart)
            .IsRequired();

        builder.Property(x => x.CurrentPeriodEnd)
            .IsRequired();

        builder.Property(x => x.NextBillingDate)
            .IsRequired(false);

        builder.Property(x => x.AutoRenew)
            .IsRequired();

        builder.Property(x => x.LastInvoiceGeneratedAt)
            .IsRequired(false);

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubscriptionPlan)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubscriptionPlanId);

        builder.HasIndex(x => x.TenantId)
            .HasFilter("[IsActive] = 1")
            .IsUnique();
    }
}