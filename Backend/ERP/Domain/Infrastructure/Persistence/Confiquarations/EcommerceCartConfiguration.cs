using Domain.Features.Ecommerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domain.Infrastructure.Persistence.Configurations;

public class EcommerceCartConfiguration : IEntityTypeConfiguration<EcommerceCart>
{
    public void Configure(EntityTypeBuilder<EcommerceCart> builder)
    {
        builder.ToTable("EcommerceCarts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.CompanyId)
            .IsRequired();

        builder.Property(x => x.CustomerId);

        builder.Property(x => x.CustomerContactId);

        builder.Property(x => x.SessionId)
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.SalesOrderId);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128);

        builder.Property(x => x.CheckoutAttemptedAtUtc);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        // Indexes for fast retrieval by customer or session within tenant/company
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.SessionId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.IdempotencyKey });

        // Relationships
        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Lines)
            .WithOne(x => x.Cart)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backing field for lines collection
        builder.Navigation(x => x.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
