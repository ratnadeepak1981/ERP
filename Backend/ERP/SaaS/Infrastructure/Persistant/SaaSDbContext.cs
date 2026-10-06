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

    // =========================
    // Tenant
    // =========================

    public DbSet<Tenant> Tenants
        => Set<Tenant>();


    // =========================
    // Subscription
    // =========================

    public DbSet<SubscriptionParameter> SubscriptionParameters
        => Set<SubscriptionParameter>();

    public DbSet<SubscriptionPlan> SubscriptionPlans
        => Set<SubscriptionPlan>();

    public DbSet<SubscriptionLimit> SubscriptionLimits
        => Set<SubscriptionLimit>();

    public DbSet<SubscriptionPlanParameter> SubscriptionPlanParameters
        => Set<SubscriptionPlanParameter>();

    public DbSet<Subscription> Subscriptions
        => Set<Subscription>();

    public DbSet<SubscriptionUsage> SubscriptionUsages
        => Set<SubscriptionUsage>();


    // =========================
    // Tenant Database
    // =========================

    public DbSet<TenantDatabase> TenantDatabases
        => Set<TenantDatabase>();

    public DbSet<TenantConfiguration> TenantConfigurations
        => Set<TenantConfiguration>();


    // =========================
    // Auditing
    // =========================

    public DbSet<PlatformAuditRecord> PlatformAuditRecords
        => Set<PlatformAuditRecord>();


    // =========================
    // EF Core Model Configuration
    // =========================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SaaSDbContext).Assembly);
    }
}