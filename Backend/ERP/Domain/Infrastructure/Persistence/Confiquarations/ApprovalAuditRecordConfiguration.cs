using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class ApprovalAuditRecordConfiguration : IEntityTypeConfiguration<Features.Procurement.Approval.ApprovalAuditRecord>
{
    public void Configure(EntityTypeBuilder<Features.Procurement.Approval.ApprovalAuditRecord> builder)
    {
        builder.ToTable("ApprovalAuditRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ActorUserName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.SnapshotAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Decision)
            .IsRequired();

        builder.Property(x => x.DecisionDateUtc)
            .IsRequired();

        builder.Property(x => x.IsAutomated)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.DocumentType,
            x.DocumentId
        });
    }
}
