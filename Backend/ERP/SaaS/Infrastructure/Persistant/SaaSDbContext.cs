using Microsoft.EntityFrameworkCore;
using SaaS.Core.Models;

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

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasMany<Subscription>()
                .WithOne(x => x.Tenant)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.SubscriptionName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.SubscriptionType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.StartDate)
                .IsRequired();

            entity.Property(x => x.EndDate);

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => x.TenantId);

            entity.HasMany<SubscriptionLimit>()
                .WithOne(x => x.Subscription)
                .HasForeignKey(x => x.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubscriptionLimit>(entity =>
        {
            entity.ToTable("SubscriptionLimits");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => x.SubscriptionId);
        });

        modelBuilder.Entity<SubscriptionLimitParameter>(entity =>
        {
            entity.ToTable("SubscriptionLimitParameters");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ParameterKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.LimitValue)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.IsUnlimited)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasOne(x => x.SubscriptionLimit)
                .WithMany(x => x.Parameters)
                .HasForeignKey(x => x.SubscriptionLimitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.SubscriptionLimitId);

            entity.HasIndex(x => new
            {
                x.SubscriptionLimitId,
                x.ParameterKey
            })
            .IsUnique();
        });

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(SaaSDbContext).Assembly);
    }
}