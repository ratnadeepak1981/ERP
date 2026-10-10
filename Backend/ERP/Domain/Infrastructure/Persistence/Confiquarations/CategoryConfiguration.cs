using ERP.Domain.Features.MasterData.Category;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CategoryConfiguration
    : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(x => x.CategoryId);

        builder.Property(x => x.CategoryCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CategoryName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Tenant + Code must be unique
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CategoryCode
        })
        .IsUnique();

        // Tenant + Name must be unique
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CategoryName
        })
        .IsUnique();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CategoryId
        })
        .IsUnique();
    }
}