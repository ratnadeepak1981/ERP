using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SaaS.Core.Models;

namespace SaaS.Application.Interfaces;

public interface ITenantDomainRepository
{
    Task<TenantDomain?> GetByHostnameAsync(string hostname);

    Task<TenantDomain?> GetBySubdomainAsync(string subdomain);

    Task<List<TenantDomain>> GetByTenantIdAsync(Guid tenantId);

    Task<bool> ExistsHostnameAsync(string hostname);

    Task<bool> ExistsSubdomainAsync(string subdomain);

    Task AddAsync(TenantDomain tenantDomain);

    Task SaveChangesAsync();
}
