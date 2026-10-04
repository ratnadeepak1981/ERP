using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Security.Core.Models;

namespace Security.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration
    : IEntityTypeConfiguration<UserRole>
{
    public void Configure(
        EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.User)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => x.RoleId);

        builder.HasIndex(x => new
        {
            x.UserId,
            x.RoleId
        })
        .IsUnique();

        builder.HasMany(x => x.Scopes)
            .WithOne(x => x.UserRole)
            .HasForeignKey(x => x.UserRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}