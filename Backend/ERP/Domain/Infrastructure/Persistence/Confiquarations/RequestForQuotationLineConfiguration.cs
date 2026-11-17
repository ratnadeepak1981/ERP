using Domain.Features.Procurement.RequestForQuotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class RequestForQuotationLineConfiguration : IEntityTypeConfiguration<RequestForQuotationLine>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationLine> builder)
    {
        builder.ToTable("RequestForQuotationLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.UnitOfMeasureCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.TargetUnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.TechnicalSpecifications)
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasIndex(x => new { x.RFQId, x.LineNumber })
            .IsUnique()
            .HasDatabaseName("UK_RequestForQuotationLines_RFQ_LineNumber");
    }
}
