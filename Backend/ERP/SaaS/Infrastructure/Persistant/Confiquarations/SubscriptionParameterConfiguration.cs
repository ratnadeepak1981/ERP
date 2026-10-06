using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class SubscriptionParameterConfiguration
    : IEntityTypeConfiguration<SubscriptionParameter>
{
    public void Configure(
        EntityTypeBuilder<SubscriptionParameter> builder)
    {
        builder.ToTable("SubscriptionParameters");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParameterKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ParameterType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // ParameterKey must be unique
        builder.HasIndex(x => x.ParameterKey)
            .IsUnique();
    }
}