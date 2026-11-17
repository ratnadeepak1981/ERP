using Domain.Features.Procurement.RequestForQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> builder)
    {
        builder.ToTable("RequestForQuotations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RFQNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.ClosingDate)
            .IsRequired();

        builder.Property(x => x.DeliveryRequiredDate)
            .IsRequired(false);

        builder.Property(x => x.DeliveryTerms)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.Instructions)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.RFQNumber })
            .IsUnique()
            .HasDatabaseName("UK_RequestForQuotations_Tenant_Company_RFQNumber");

        builder.HasIndex(x => new { x.TenantId, x.Status, x.ClosingDate })
            .HasDatabaseName("IX_RequestForQuotations_Tenant_Status_ClosingDate");

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.RequestForQuotation)
            .HasForeignKey(x => x.RFQId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.InvitedSuppliers)
            .WithOne(x => x.RequestForQuotation)
            .HasForeignKey(x => x.RFQId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Quotations)
            .WithOne(x => x.RequestForQuotation)
            .HasForeignKey(x => x.RFQId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
