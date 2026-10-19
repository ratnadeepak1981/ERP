using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.CRM;

public class OpportunityRepository : IOpportunityRepository
{
    private readonly DomainDbContext _context;

    public OpportunityRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Opportunity>> GetOpportunitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? customerId = null,
        OpportunityStage? stage = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Opportunities
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.IsActive);

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (stage.HasValue)
        {
            query = query.Where(x => x.Stage == stage.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Opportunity?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid opportunityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Opportunities
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.OpportunityId == opportunityId, cancellationToken);
    }

    public async Task<bool> ExistsOpportunityNumberAsync(
        Guid tenantId,
        Guid companyId,
        string opportunityNumber,
        CancellationToken cancellationToken = default)
    {
        string norm = opportunityNumber.Trim();
        return await _context.Opportunities
            .AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.OpportunityNumber == norm, cancellationToken);
    }

    public async Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        await _context.Opportunities.AddAsync(opportunity, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
