using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionUsageConfiguration
    : IEntityTypeConfiguration<SubscriptionUsage>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionUsage> builder)
    {
        builder.ToTable("SubscriptionUsages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UsageValue)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.PeriodStart)
            .IsRequired();

        builder.Property(x => x.PeriodEnd)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        builder.HasOne(x => x.Subscription)
            .WithMany(x => x.Usages)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubscriptionPlanParameter)
            .WithMany(x => x.Usages)
            .HasForeignKey(x => x.SubscriptionPlanParameterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubscriptionId);

        builder.HasIndex(x => x.SubscriptionPlanParameterId);

        builder.HasIndex(x => new
        {
            x.SubscriptionId,
            x.SubscriptionPlanParameterId,
            x.PeriodStart,
            x.PeriodEnd
        })
        .IsUnique();
    }
}