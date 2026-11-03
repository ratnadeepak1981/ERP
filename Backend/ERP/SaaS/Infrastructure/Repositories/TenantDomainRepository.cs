using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Interfaces;
using SaaS.Core.Models;
using SaaS.Core.Rules;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Infrastructure.Repositories;

public class TenantDomainRepository : ITenantDomainRepository
{
    private readonly SaaSDbContext _context;

    public TenantDomainRepository(SaaSDbContext context)
    {
        _context = context;
    }

    public async Task<TenantDomain?> GetByHostnameAsync(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return null;
        }

        string normalized = TenantDomainRules.NormalizeHostname(hostname);

        return await _context.TenantDomains
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Hostname == normalized);
    }

    public async Task<TenantDomain?> GetBySubdomainAsync(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return null;
        }

        string normalized = TenantDomainRules.NormalizeSubdomain(subdomain);

        return await _context.TenantDomains
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Subdomain == normalized && x.IsActive);
    }

    public async Task<List<TenantDomain>> GetByTenantIdAsync(Guid tenantId)
    {
        return await _context.TenantDomains
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .ToListAsync();
    }

    public async Task<bool> ExistsHostnameAsync(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        string normalized = TenantDomainRules.NormalizeHostname(hostname);

        return await _context.TenantDomains
            .AnyAsync(x => x.Hostname == normalized);
    }

    public async Task<bool> ExistsSubdomainAsync(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            return false;
        }

        string normalized = TenantDomainRules.NormalizeSubdomain(subdomain);

        return await _context.TenantDomains
            .AnyAsync(x => x.Subdomain == normalized);
    }

    public async Task AddAsync(TenantDomain tenantDomain)
    {
        await _context.TenantDomains.AddAsync(tenantDomain);
    }

    public Task DeleteAsync(TenantDomain tenantDomain)
    {
        _context.TenantDomains.Remove(tenantDomain);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
