using Domain.Features.MasterData.Contact;
using ERP.Domain.Features.MasterData.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
{
    public void Configure(EntityTypeBuilder<SupplierContact> builder)
    {
        builder.ToTable("SupplierContacts");

        builder.HasKey(x => x.SupplierContactId);

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact contact and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId,
            x.ContactId,
            x.ContactTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active primary contact per contact type for a supplier
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.SupplierId,
            x.ContactTypeId
        })
        .IsUnique()
        .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1");

        // SupplierContact → Supplier (Tenant-aware composite foreign key)
        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SupplierId })
            .HasPrincipalKey(x => new { x.TenantId, x.SupplierId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierContact → Contact (Tenant-aware composite foreign key)
        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ContactId })
            .HasPrincipalKey(x => new { x.TenantId, x.ContactId })
            .OnDelete(DeleteBehavior.Restrict);

        // SupplierContact → ContactType
        builder.HasOne<ContactType>()
            .WithMany()
            .HasForeignKey(x => x.ContactTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
