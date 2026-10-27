using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Security.Core.Models;

namespace Security.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceName)
            .HasMaxLength(200);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(50);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.RevokedReason)
            .HasMaxLength(200);

        builder.HasOne(x => x.User)
            .WithMany(u => u.UserSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.TokenFamilyId);
        builder.HasIndex(x => new { x.UserId, x.IsRevoked, x.AbsoluteExpiresAt });
    }
}
