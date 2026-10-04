using Microsoft.EntityFrameworkCore;
using Security.Core.Models;

namespace Security.Infrastructure.Persistence;

public class SecurityDbContext : DbContext
{
    public SecurityDbContext(
        DbContextOptions<SecurityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions
        => Set<RolePermission>();

    public DbSet<UserRoleScope> UserRoleScopes
        => Set<UserRoleScope>();
}