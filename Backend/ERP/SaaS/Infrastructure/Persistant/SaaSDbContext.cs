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
    // Commercial Billing & Ledger
    // =========================

    public DbSet<TenantBillingProfile> TenantBillingProfiles
        => Set<TenantBillingProfile>();

    public DbSet<SubscriptionInvoice> SubscriptionInvoices
        => Set<SubscriptionInvoice>();

    public DbSet<SubscriptionInvoiceLine> SubscriptionInvoiceLines
        => Set<SubscriptionInvoiceLine>();

    public DbSet<PaymentTransaction> PaymentTransactions
        => Set<PaymentTransaction>();

    public DbSet<PaymentAllocation> PaymentAllocations
        => Set<PaymentAllocation>();

    public DbSet<BillingLedgerEntry> BillingLedgerEntries
        => Set<BillingLedgerEntry>();

    public DbSet<CreditNote> CreditNotes
        => Set<CreditNote>();

    public DbSet<RefundTransaction> RefundTransactions
        => Set<RefundTransaction>();


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


    public override int SaveChanges()
    {
        ValidateTenantConsistency();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateTenantConsistency();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ValidateTenantConsistency()
    {
        // 1. SubscriptionInvoiceLines must agree with SubscriptionInvoice.TenantId
        foreach (var entry in ChangeTracker.Entries<SubscriptionInvoiceLine>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var invoice = entry.Entity.SubscriptionInvoice ?? SubscriptionInvoices.Find(entry.Entity.SubscriptionInvoiceId);
            if (invoice != null && invoice.TenantId != entry.Entity.TenantId)
            {
                throw new InvalidOperationException($"Tenant mismatch: Invoice line tenant '{entry.Entity.TenantId}' does not match invoice tenant '{invoice.TenantId}'.");
            }
        }

        // 2. PaymentAllocations must agree with both PaymentTransaction.TenantId and SubscriptionInvoice.TenantId
        foreach (var entry in ChangeTracker.Entries<PaymentAllocation>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var invoice = entry.Entity.SubscriptionInvoice ?? SubscriptionInvoices.Find(entry.Entity.SubscriptionInvoiceId);
            var payment = entry.Entity.PaymentTransaction ?? PaymentTransactions.Find(entry.Entity.PaymentTransactionId);

            if (invoice != null && invoice.TenantId != entry.Entity.TenantId)
            {
                throw new InvalidOperationException($"Tenant mismatch: Allocation tenant '{entry.Entity.TenantId}' does not match invoice tenant '{invoice.TenantId}'.");
            }

            if (payment != null && payment.TenantId != entry.Entity.TenantId)
            {
                throw new InvalidOperationException($"Tenant mismatch: Allocation tenant '{entry.Entity.TenantId}' does not match payment tenant '{payment.TenantId}'.");
            }
        }

        // 3. CreditNotes must agree with SubscriptionInvoice.TenantId
        foreach (var entry in ChangeTracker.Entries<CreditNote>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var invoice = entry.Entity.SubscriptionInvoice ?? SubscriptionInvoices.Find(entry.Entity.SubscriptionInvoiceId);
            if (invoice != null && invoice.TenantId != entry.Entity.TenantId)
            {
                throw new InvalidOperationException($"Tenant mismatch: Credit note tenant '{entry.Entity.TenantId}' does not match invoice tenant '{invoice.TenantId}'.");
            }
        }

        // 4. RefundTransactions must agree with PaymentTransaction.TenantId
        foreach (var entry in ChangeTracker.Entries<RefundTransaction>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var payment = entry.Entity.PaymentTransaction ?? PaymentTransactions.Find(entry.Entity.PaymentTransactionId);
            if (payment != null && payment.TenantId != entry.Entity.TenantId)
            {
                throw new InvalidOperationException($"Tenant mismatch: Refund tenant '{entry.Entity.TenantId}' does not match payment tenant '{payment.TenantId}'.");
            }
        }
    }

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