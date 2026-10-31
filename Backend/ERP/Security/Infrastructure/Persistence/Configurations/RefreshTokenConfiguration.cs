using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Security.Core.Models;

namespace Security.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.IsConsumed)
            .IsConcurrencyToken();

        builder.Property(x => x.RevokedReason)
            .HasMaxLength(200);

        builder.Property(x => x.ReplacedByTokenHash)
            .HasMaxLength(128);

        builder.HasOne(x => x.UserSession)
            .WithMany(s => s.RefreshTokens)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash)
            .IsUnique();

        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.TokenFamilyId);
        builder.HasIndex(x => new { x.SessionId, x.IsConsumed, x.IsRevoked, x.ExpiresAt });
    }
}
