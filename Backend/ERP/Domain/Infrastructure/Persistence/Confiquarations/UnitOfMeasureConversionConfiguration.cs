using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class UnitOfMeasureConversionConfiguration : IEntityTypeConfiguration<UnitOfMeasureConversion>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasureConversion> builder)
    {
        builder.ToTable("UnitOfMeasureConversions");

        builder.HasKey(x => x.ConversionId);

        builder.Property(x => x.ConversionFactor)
            .IsRequired()
            .HasColumnType("decimal(18,6)");

        builder.Property(x => x.IsActive)
            .IsRequired();

        // One conversion rule per pair of units within a tenant
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.FromUnitId,
            x.ToUnitId
        })
        .IsUnique();

        // FromUnit (Tenant-aware composite foreign key)
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.FromUnitId })
            .HasPrincipalKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.Restrict);

        // ToUnit (Tenant-aware composite foreign key)
        builder.HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ToUnitId })
            .HasPrincipalKey(x => new { x.TenantId, x.UnitOfMeasureId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
