using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Security.Infrastructure.Persistence.Auditing;

namespace Security.Infrastructure.Persistence.Configurations;

public class SecurityAuditRecordConfiguration
    : IEntityTypeConfiguration<SecurityAuditRecord>
{
    public void Configure(
        EntityTypeBuilder<SecurityAuditRecord> builder)
    {
        builder.ToTable("SecurityAuditRecords");

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

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.EntityName,
            x.EntityId
        });

        builder.HasIndex(x => x.Timestamp);
    }
}