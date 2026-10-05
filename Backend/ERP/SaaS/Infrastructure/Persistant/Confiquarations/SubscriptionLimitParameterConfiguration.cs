using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionLimitParameterConfiguration
    : IEntityTypeConfiguration<SubscriptionLimitParameter>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionLimitParameter> builder)
    {
        builder.ToTable("SubscriptionLimitParameters");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParameterKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LimitValue)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.IsUnlimited)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // SubscriptionLimit → Parameters
        builder.HasOne(x => x.SubscriptionLimit)
            .WithMany(x => x.Parameters)
            .HasForeignKey(x => x.SubscriptionLimitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Fast lookup
        builder.HasIndex(x => x.SubscriptionLimitId);

        // Prevent duplicate parameter keys
        // within the same subscription limit
        builder.HasIndex(x => new
        {
            x.SubscriptionLimitId,
            x.ParameterKey
        })
        .IsUnique();
    }
}