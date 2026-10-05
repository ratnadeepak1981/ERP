using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Infrastructure.Persistence.Auditing;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class PlatformAuditRecordConfiguration
    : IEntityTypeConfiguration<PlatformAuditRecord>
{
    public void Configure(
        EntityTypeBuilder<PlatformAuditRecord> builder)
    {
        builder.ToTable("PlatformAuditRecords");

        builder.HasKey(x => x.AuditId);

        builder.Property(x => x.EntityName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.OldValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.NewValues)
            .HasColumnType("nvarchar(max)");

        // Tenant + entity lookup
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.EntityName,
            x.EntityId
        });

        // Time-based audit queries
        builder.HasIndex(x => x.Timestamp);
    }
}