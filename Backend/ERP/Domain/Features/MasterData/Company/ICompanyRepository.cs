using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.Company;

public interface ICompanyRepository
{
    Task<List<Company>> GetByTenantAsync(Guid tenantId);

    Task<Company?> GetByIdAsync(Guid tenantId, Guid id);

    Task AddAsync(Company company);

    Task UpdateAsync(Company company);

    Task SaveChangesAsync();
}
