using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Company;

public class CompanyRepository : ICompanyRepository
{
    private readonly DomainDbContext _context;

    public CompanyRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Company>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Companies
            .Include(x => x.BaseCurrency)
            .Where(x => x.TenantId == tenantId)
            .ToListAsync();
    }

    public async Task<Company?> GetByIdAsync(Guid tenantId, Guid id)
    {
        return await _context.Companies
            .Include(x => x.BaseCurrency)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId);
    }

    public async Task AddAsync(Company company)
    {
        await _context.Companies.AddAsync(company);
    }

    public Task UpdateAsync(Company company)
    {
        _context.Companies.Update(company);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
