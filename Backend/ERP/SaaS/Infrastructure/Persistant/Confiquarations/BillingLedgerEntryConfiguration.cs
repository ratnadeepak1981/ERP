using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class BillingLedgerEntryConfiguration : IEntityTypeConfiguration<BillingLedgerEntry>
{
    public void Configure(EntityTypeBuilder<BillingLedgerEntry> builder)
    {
        builder.ToTable("BillingLedgerEntries", table =>
        {
            table.HasCheckConstraint("CK_BillingLedgerEntries_Amount_Positive", "[Amount] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.EntryType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Direction)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.PostedAtUtc)
            .IsRequired();

        builder.Property(x => x.SourceDocumentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SourceDocumentId)
            .IsRequired();

        builder.Property(x => x.ReferenceNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(250);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Duplicate-posting protection index: A source document + entry type + direction cannot be posted twice for a tenant
        builder.HasIndex(x => new { x.TenantId, x.SourceDocumentType, x.SourceDocumentId, x.EntryType, x.Direction })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.PostedAtUtc });
    }
}
