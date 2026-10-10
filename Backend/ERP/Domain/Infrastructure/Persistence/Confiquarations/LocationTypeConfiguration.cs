using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class LocationTypeConfiguration : IEntityTypeConfiguration<LocationType>
{
    public void Configure(EntityTypeBuilder<LocationType> builder)
    {
        builder.ToTable("LocationTypes");

        builder.HasKey(x => x.LocationTypeId);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.Property(x => x.CanStoreInventory)
            .IsRequired();

        builder.Property(x => x.CanPick)
            .IsRequired();

        builder.Property(x => x.CanPutAway)
            .IsRequired();

        builder.Property(x => x.CanContainChildren)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Code
        })
        .IsUnique();
    }
}
