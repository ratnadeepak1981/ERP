using Domain.Features.Procurement.RequestForQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class RFQSupplierConfiguration : IEntityTypeConfiguration<RFQSupplier>
{
    public void Configure(EntityTypeBuilder<RFQSupplier> builder)
    {
        builder.ToTable("RFQSuppliers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SupplierCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.SupplierName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ContactEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.InvitationStatus)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.HasIndex(x => new { x.RFQId, x.SupplierId })
            .IsUnique()
            .HasDatabaseName("UK_RFQSuppliers_RFQ_SupplierId");

        builder.HasIndex(x => x.TokenHash)
            .HasDatabaseName("IX_RFQSuppliers_TokenHash");
    }
}
