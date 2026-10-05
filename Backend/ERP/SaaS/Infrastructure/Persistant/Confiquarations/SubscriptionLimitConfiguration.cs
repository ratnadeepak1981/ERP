using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionLimitConfiguration
    : IEntityTypeConfiguration<SubscriptionLimit>
{
    public void Configure(EntityTypeBuilder<SubscriptionLimit> builder)
    {
        builder.ToTable("SubscriptionLimits");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SubscriptionId);
    }
}