using Microsoft.EntityFrameworkCore;
using Security.Core.Models;
using Security.Infrastructure.Persistence.Auditing;

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

    public DbSet<Permission> Permissions
        => Set<Permission>();

    public DbSet<UserRole> UserRoles
        => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions
        => Set<RolePermission>();

    public DbSet<UserRoleScope> UserRoleScopes
        => Set<UserRoleScope>();

    public DbSet<SecurityAuditRecord>
        SecurityAuditRecords
        => Set<SecurityAuditRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SecurityDbContext).Assembly);
    }
}