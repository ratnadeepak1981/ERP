using Domain.Features.Ecommerce;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class EcommerceProductConfiguration : IEntityTypeConfiguration<EcommerceProduct>
{
    public void Configure(EntityTypeBuilder<EcommerceProduct> builder)
    {
        builder.ToTable("EcommerceProducts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CompanyId)
            .IsRequired();

        builder.Property(x => x.ProductId)
            .IsRequired();

        builder.Property(x => x.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.OnlineDescription)
            .HasMaxLength(4000);

        builder.Property(x => x.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        // Unique constraint: A product can only have one publication record per Selling Company within a Tenant
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId })
            .IsUnique();

        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IsPublished });
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.DisplayOrder });

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
