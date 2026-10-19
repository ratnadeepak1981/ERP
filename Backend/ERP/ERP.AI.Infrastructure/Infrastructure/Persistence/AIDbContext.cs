using ERP.AI.Infrastructure.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Infrastructure.Persistence;

public class AIDbContext : DbContext
{
    public AIDbContext(DbContextOptions<AIDbContext> options)
        : base(options)
    {
    }

    public DbSet<AIProvider> AIProviders => Set<AIProvider>();

    public DbSet<AIModel> AIModels => Set<AIModel>();

    public DbSet<AIProviderKeyReference> AIProviderKeyReferences => Set<AIProviderKeyReference>();

    public DbSet<AIProviderCredential> AIProviderCredentials => Set<AIProviderCredential>();

    public DbSet<TenantAIConfiguration> TenantAIConfigurations => Set<TenantAIConfiguration>();

    public DbSet<AIUsageRecord> AIUsageRecords => Set<AIUsageRecord>();

    public DbSet<AIAuditRecord> AIAuditRecords => Set<AIAuditRecord>();

    public override int SaveChanges()
    {
        ValidateTenantAIConfigurationModelConsistency();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateTenantAIConfigurationModelConsistency();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateTenantAIConfigurationModelConsistency()
    {
        // Enforce that selected Model belongs to the selected Provider
        var tenantConfigs = ChangeTracker.Entries<TenantAIConfiguration>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var config in tenantConfigs)
        {
            var model = config.Model ?? AIModels.Find(config.AIModelId);
            if (model != null && model.AIProviderId != config.AIProviderId)
            {
                throw new InvalidOperationException(
                    $"AI model consistency violation: Model '{config.AIModelId}' belongs to provider '{model.AIProviderId}', but configuration specifies provider '{config.AIProviderId}'.");
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AIDbContext).Assembly);
    }
}
