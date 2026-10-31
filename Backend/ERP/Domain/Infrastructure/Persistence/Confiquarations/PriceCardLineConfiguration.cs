using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class PriceCardLineConfiguration : IEntityTypeConfiguration<PriceCardLine>
{
    public void Configure(EntityTypeBuilder<PriceCardLine> builder)
    {
        builder.ToTable("PriceCardLines");

        builder.HasKey(x => x.PriceCardLineId);

        builder.Property(x => x.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,4)");

        builder.Property(x => x.MinimumQuantity)
            .IsRequired()
            .HasColumnType("decimal(18,4)")
            .HasDefaultValue(1m);

        builder.Property(x => x.EffectiveFrom)
            .IsRequired(false);

        builder.Property(x => x.EffectiveTo)
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Index for lookups
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PriceCardId,
            x.ProductId
        });

        // Unique constraint preventing duplicate Product + UOM per Price Card
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PriceCardId,
            x.ProductId,
            x.UnitOfMeasureId
        })
        .IsUnique();

        // Foreign keys
        builder.HasOne(x => x.PriceCard)
            .WithMany(p => p.Lines)
            .HasForeignKey(x => x.PriceCardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
