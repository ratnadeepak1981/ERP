using ERP.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.Auditing;

public class DomainAuditRecordConfiguration
    : IEntityTypeConfiguration<DomainAuditRecord>
{
    public void Configure(
        EntityTypeBuilder<DomainAuditRecord> builder)
    {
        builder.ToTable("DomainAuditRecords");

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