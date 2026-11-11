using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.MasterData;

public class BranchRepository : IBranchRepository
{
    private readonly DomainDbContext _context;

    public BranchRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Branch>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId)
    {
        return await _context.Branches
            .Where(b =>
                b.CompanyId == companyId &&
                _context.Companies.Any(c =>
                    c.Id == b.CompanyId &&
                    c.TenantId == tenantId))
            .ToListAsync();
    }

    public async Task<Branch?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        return await _context.Branches
            .Where(b =>
                b.Id == branchId &&
                b.CompanyId == companyId &&
                _context.Companies.Any(c =>
                    c.Id == b.CompanyId &&
                    c.TenantId == tenantId))
            .FirstOrDefaultAsync();
    }

    public async Task<Branch> AddAsync(Branch branch)
    {
        await _context.Branches.AddAsync(branch);
        return branch;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
