using Domain.Features.MasterData.Brand;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Confiquarations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");

        builder.HasKey(b => b.BrandId);

        builder.Property(b => b.BrandCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.BrandName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.Description)
            .HasMaxLength(500);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(b => new { b.TenantId, b.BrandCode })
            .IsUnique();

        builder.HasIndex(b => new { b.TenantId, b.BrandId })
            .IsUnique();
    }
}
