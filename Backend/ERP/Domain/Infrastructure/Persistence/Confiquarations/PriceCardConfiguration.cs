using Domain.Features.MasterData.PriceCard;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class PriceCardConfiguration : IEntityTypeConfiguration<PriceCard>
{
    public void Configure(EntityTypeBuilder<PriceCard> builder)
    {
        builder.ToTable("PriceCards");

        builder.HasKey(x => x.PriceCardId);

        builder.Property(x => x.PriceCardCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PriceCardName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PriceCardType)
            .IsRequired();

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.EffectiveFrom)
            .IsRequired();

        builder.Property(x => x.EffectiveTo)
            .IsRequired(false);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Unique index per tenant on PriceCardCode
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PriceCardCode
        })
        .IsUnique();

        // Index for querying by Tenant + Type + Active
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PriceCardType,
            x.IsActive
        });

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.PriceCardId
        })
        .IsUnique();

        // 1-to-many relationship with PriceCardLines
        builder.HasMany(x => x.Lines)
            .WithOne(l => l.PriceCard)
            .HasForeignKey(l => l.PriceCardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
