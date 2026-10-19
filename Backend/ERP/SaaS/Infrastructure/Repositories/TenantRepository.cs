using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly SaaSDbContext _context;

    public TenantRepository(SaaSDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
    }

    public async Task<Tenant?> GetByIdAsync(Guid tenantId)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId);
    }

    public async Task<bool> ExistsByCodeAsync(string code)
    {
        return await _context.Tenants
            .AnyAsync(x => x.Code == code);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync(
        IDbContextTransaction transaction)
    {
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
    }

    public async Task RollbackTransactionAsync(
        IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }

    public async Task<List<SaaS.Application.DTOs.TenantQueueItemDto>> GetTenantListAsync()
    {
        return await (from tenant in _context.Tenants.AsNoTracking()
                      join sub in _context.Subscriptions.AsNoTracking()
                          on tenant.Id equals sub.TenantId into subs
                      from sub in subs.DefaultIfEmpty()
                      join td in _context.TenantDatabases.AsNoTracking()
                          on tenant.Id equals td.TenantId into dbs
                      from td in dbs.DefaultIfEmpty()
                      orderby tenant.CreatedAt descending
                      select new SaaS.Application.DTOs.TenantQueueItemDto
                      {
                          Id = tenant.Id,
                          Name = tenant.Name,
                          Code = tenant.Code,
                          IsActive = tenant.IsActive,
                          CreatedAt = tenant.CreatedAt,
                          PlanName = sub != null && !string.IsNullOrEmpty(sub.SubscriptionName) ? sub.SubscriptionName : "Manufacturing Cloud",
                          SubscriptionStatus = sub != null ? sub.Status.ToString() : "None",
                          StorageMode = sub != null ? sub.StorageMode.ToString() : "None",
                          Status = (td != null && td.IsActive) ? "Completed" : "Pending",
                          IsProvisioned = td != null && td.IsActive,
                          DatabaseName = td != null ? td.DatabaseName : null
                      }).ToListAsync();
    }

    public async Task<SaaS.Application.DTOs.TenantDetailsDto?> GetTenantDetailsAsync(Guid tenantId)
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) return null;

        var sub = await _context.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var db = await _context.TenantDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.TenantId == tenantId);
        var config = await _context.TenantConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId);

        return new SaaS.Application.DTOs.TenantDetailsDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Code = tenant.Code,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            ModifiedAt = tenant.ModifiedAt,
            SubscriptionId = sub?.Id,
            SubscriptionPlanId = sub?.SubscriptionPlanId,
            SubscriptionName = sub?.SubscriptionName ?? string.Empty,
            SubscriptionType = sub?.SubscriptionType ?? string.Empty,
            SubscriptionStatus = sub != null ? sub.Status.ToString() : "None",
            StorageMode = sub != null ? sub.StorageMode.ToString() : "None",
            SubscriptionStartDate = sub?.StartDate,
            SubscriptionEndDate = sub?.EndDate,
            IsProvisioned = db != null && db.IsActive,
            DatabaseName = db?.DatabaseName,
            DatabaseServer = db?.DatabaseServer,
            DatabaseIsActive = db?.IsActive ?? false,
            ConfigurationExists = config != null
        };
    }

    public async Task<SaaS.Application.DTOs.TenantLifecycleStatusDto?> GetTenantStatusAsync(Guid tenantId)
    {
        var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) return null;

        var sub = await _context.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var db = await _context.TenantDatabases.AsNoTracking().FirstOrDefaultAsync(d => d.TenantId == tenantId);
        var config = await _context.TenantConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId);

        bool isProvisioned = db != null && db.IsActive && config != null;

        return new SaaS.Application.DTOs.TenantLifecycleStatusDto
        {
            TenantId = tenant.Id,
            IsRegistered = true,
            HasActiveSubscription = sub != null && sub.IsActive,
            SubscriptionStatus = sub != null ? sub.Status.ToString() : "None",
            StorageMode = sub != null ? sub.StorageMode.ToString() : "None",
            DatabaseAllocated = db != null && db.IsActive,
            DatabaseName = db?.DatabaseName,
            DatabaseServer = db?.DatabaseServer,
            ConfigurationAllocated = config != null,
            IsProvisioned = isProvisioned,
            OverallStatus = isProvisioned ? "Completed" : "Pending"
        };
    }

    public async Task<SaaS.Application.DTOs.TenantMetricsDto> GetTenantMetricsAsync()
    {
        int total = await _context.Tenants.CountAsync();
        int activeSubs = await _context.Subscriptions.CountAsync(s => s.IsActive);
        int completed = await _context.TenantDatabases.CountAsync(d => d.IsActive);
        int pending = Math.Max(0, total - completed);

        return new SaaS.Application.DTOs.TenantMetricsDto
        {
            TotalRegistered = total,
            Pending = pending,
            InProgress = 0,
            Failed = 0,
            Completed = completed,
            ActiveSubscriptions = activeSubs,
            IsLive = true
        };
    }
}