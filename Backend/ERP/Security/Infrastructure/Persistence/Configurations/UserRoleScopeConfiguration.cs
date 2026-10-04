using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Security.Core.Models;

namespace Security.Infrastructure.Persistence.Configurations;

public class UserRoleScopeConfiguration
    : IEntityTypeConfiguration<UserRoleScope>
{
    public void Configure(
        EntityTypeBuilder<UserRoleScope> builder)
    {
        builder.ToTable("UserRoleScopes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasOne(x => x.UserRole)
            .WithMany(x => x.Scopes)
            .HasForeignKey(x => x.UserRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserRoleId);

        builder.HasIndex(x => new
        {
            x.UserRoleId,
            x.CompanyId,
            x.BranchId
        })
        .IsUnique();
    }
}