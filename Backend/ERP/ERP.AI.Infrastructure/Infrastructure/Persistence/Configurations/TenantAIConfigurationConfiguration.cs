using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Configurations;

public class TenantAIConfigurationConfiguration : IEntityTypeConfiguration<TenantAIConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantAIConfiguration> builder)
    {
        builder.ToTable("TenantAIConfigurations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.AIProviderId)
            .IsRequired();

        builder.Property(x => x.AIModelId)
            .IsRequired();

        builder.Property(x => x.AIProviderKeyReferenceId);

        builder.Property(x => x.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        builder.Property(x => x.UpdatedByUserId);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Enforce uniqueness: one active default AI configuration per tenant
        builder.HasIndex(x => x.TenantId)
            .IsUnique();

        // Navigations within AI database
        builder.HasOne(x => x.Provider)
            .WithMany()
            .HasForeignKey(x => x.AIProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Model)
            .WithMany()
            .HasForeignKey(x => x.AIModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KeyReference)
            .WithMany()
            .HasForeignKey(x => x.AIProviderKeyReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
