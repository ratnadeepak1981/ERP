using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.CRM;

public class LeadRepository : ILeadRepository
{
    private readonly DomainDbContext _context;

    public LeadRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Lead>> GetLeadsAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? assignedUserId = null,
        LeadStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Leads
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.IsActive);

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (assignedUserId.HasValue)
        {
            query = query.Where(x => x.AssignedToUserId == assignedUserId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Lead?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Leads
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.LeadId == leadId, cancellationToken);
    }

    public async Task<bool> ExistsLeadNumberAsync(
        Guid tenantId,
        Guid companyId,
        string leadNumber,
        CancellationToken cancellationToken = default)
    {
        string norm = leadNumber.Trim();
        return await _context.Leads
            .AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.LeadNumber == norm, cancellationToken);
    }

    public async Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _context.Leads.AddAsync(lead, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
