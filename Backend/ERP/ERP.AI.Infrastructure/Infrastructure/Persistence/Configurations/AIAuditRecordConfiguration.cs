using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Configurations;

public class AIAuditRecordConfiguration : IEntityTypeConfiguration<AIAuditRecord>
{
    public void Configure(EntityTypeBuilder<AIAuditRecord> builder)
    {
        builder.ToTable("AIAuditRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId);

        builder.Property(x => x.ActorUserId);

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.EntityId)
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.Outcome)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Success");

        builder.Property(x => x.SafeMetadataJson)
            .HasColumnType("nvarchar(max)");

        // Indexes for fast audit queries
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.CorrelationId);
    }
}
