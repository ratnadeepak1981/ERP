using Domain.Features.MasterData.Contact;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.ToTable("CustomerContacts");

        builder.HasKey(x => x.CustomerContactId);

        builder.Property(x => x.IsPrimary)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Prevent duplicate association of exact contact and type
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId,
            x.ContactId,
            x.ContactTypeId
        })
        .IsUnique();

        // Filtered unique index: at most one active primary contact per contact type for a customer
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.CustomerId,
            x.ContactTypeId
        })
        .IsUnique()
        .HasFilter("[IsPrimary] = 1 AND [IsActive] = 1");

        // CustomerContact → Customer (Tenant-aware composite foreign key)
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.CustomerId })
            .HasPrincipalKey(x => new { x.TenantId, x.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerContact → Contact (Tenant-aware composite foreign key)
        builder.HasOne<Contact>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ContactId })
            .HasPrincipalKey(x => new { x.TenantId, x.ContactId })
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerContact → ContactType
        builder.HasOne<ContactType>()
            .WithMany()
            .HasForeignKey(x => x.ContactTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
