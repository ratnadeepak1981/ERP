using Microsoft.EntityFrameworkCore;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence.Auditing;

namespace SaaS.Infrastructure.Persistence;

public class SaaSDbContext : DbContext
{
    public SaaSDbContext(
        DbContextOptions<SaaSDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<SubscriptionLimit> SubscriptionLimits
        => Set<SubscriptionLimit>();

    public DbSet<SubscriptionLimitParameter>
        SubscriptionLimitParameters
        => Set<SubscriptionLimitParameter>();

    public DbSet<TenantDatabase> TenantDatabases
        => Set<TenantDatabase>();

    public DbSet<TenantConfiguration> TenantConfigurations
        => Set<TenantConfiguration>();

    public DbSet<PlatformAuditRecord> PlatformAuditRecords
        => Set<PlatformAuditRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SaaSDbContext).Assembly);
    }
}