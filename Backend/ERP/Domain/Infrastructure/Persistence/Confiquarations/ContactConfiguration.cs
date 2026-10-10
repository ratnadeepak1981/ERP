using Domain.Features.MasterData.Contact;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.MasterData;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");

        builder.HasKey(x => x.ContactId);

        builder.Property(x => x.ContactName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.JobTitle)
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.MobileNumber)
            .HasMaxLength(50);

        builder.Property(x => x.PreferredContactMethod)
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .IsRequired();

        // Composite key for tenant-aware foreign keys
        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ContactId
        })
        .IsUnique();
    }
}
