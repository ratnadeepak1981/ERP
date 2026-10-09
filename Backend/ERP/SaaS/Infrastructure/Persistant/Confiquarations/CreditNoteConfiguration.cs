using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Core.Models;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.ToTable("CreditNotes", table =>
        {
            table.HasCheckConstraint("CK_CreditNotes_Amount_Positive", "[Amount] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SubscriptionInvoiceId)
            .IsRequired();

        builder.Property(x => x.CreditNoteNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.IssuedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubscriptionInvoice)
            .WithMany(x => x.CreditNotes)
            .HasForeignKey(x => x.SubscriptionInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CreditNoteNumber)
            .IsUnique();

        builder.HasIndex(x => x.SubscriptionInvoiceId);
        builder.HasIndex(x => x.TenantId);
    }
}
