using Domain.Features.Ecommerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class EcommerceCartLineConfiguration : IEntityTypeConfiguration<EcommerceCartLine>
{
    public void Configure(EntityTypeBuilder<EcommerceCartLine> builder)
    {
        builder.ToTable("EcommerceCartLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CartId)
            .IsRequired();

        builder.Property(x => x.ProductId)
            .IsRequired();

        builder.Property(x => x.UnitOfMeasureId);

        builder.Property(x => x.ProductPackagingId);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        builder.HasIndex(x => x.CartId);
        builder.HasIndex(x => new { x.CartId, x.ProductId });

        // Relationships
        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(x => x.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductPackaging)
            .WithMany()
            .HasForeignKey(x => x.ProductPackagingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
