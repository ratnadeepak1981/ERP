using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Configurations;

public class AIUsageRecordConfiguration : IEntityTypeConfiguration<AIUsageRecord>
{
    public void Configure(EntityTypeBuilder<AIUsageRecord> builder)
    {
        builder.ToTable("AIUsageRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId);

        builder.Property(x => x.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.AIProviderId)
            .IsRequired();

        builder.Property(x => x.AIModelId)
            .IsRequired();

        builder.Property(x => x.AIProviderKeyReferenceId);

        builder.Property(x => x.AIApplicationCode)
            .HasMaxLength(100);

        builder.Property(x => x.AIFeatureCode)
            .HasMaxLength(100);

        builder.Property(x => x.PromptVersion)
            .HasMaxLength(50);

        builder.Property(x => x.UsageSource)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("ProviderReported");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Success");

        builder.Property(x => x.EstimatedCost)
            .HasPrecision(18, 6);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(10);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc);

        // Required indexes for performance & telemetry:
        // 1. Tenant + Time index
        builder.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });

        // 2. Provider + Model + Time index
        builder.HasIndex(x => new { x.AIProviderId, x.AIModelId, x.CreatedAtUtc });

        // 3. Key Reference + Time index
        builder.HasIndex(x => new { x.AIProviderKeyReferenceId, x.CreatedAtUtc });

        // 4. Application + Feature + Time index
        builder.HasIndex(x => new { x.AIApplicationCode, x.AIFeatureCode, x.CreatedAtUtc });

        // 5. Correlation ID index
        builder.HasIndex(x => x.CorrelationId);

        // Navigations
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
