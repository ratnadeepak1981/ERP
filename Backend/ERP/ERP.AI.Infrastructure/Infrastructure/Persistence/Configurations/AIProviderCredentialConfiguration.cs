using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence.Configurations;

public class AIProviderCredentialConfiguration : IEntityTypeConfiguration<AIProviderCredential>
{
    public void Configure(EntityTypeBuilder<AIProviderCredential> builder)
    {
        builder.ToTable("AIProviderCredentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.AIProviderId)
            .IsRequired();

        builder.Property(x => x.KeyReferenceCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EncryptedSecret)
            .IsRequired();

        builder.Property(x => x.EncryptionAlgorithm)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("RSA-4096-OAEP-SHA256");

        builder.Property(x => x.KeyVersion)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.Environment)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Production");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        builder.Property(x => x.UpdatedByUserId);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Enforce uniqueness of active credential per provider, environment, and key reference code
        builder.HasIndex(x => new { x.AIProviderId, x.KeyReferenceCode, x.Environment })
            .IsUnique();

        // Navigation to AIProvider
        builder.HasOne(x => x.Provider)
            .WithMany()
            .HasForeignKey(x => x.AIProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
