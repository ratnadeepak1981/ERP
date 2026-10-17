using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.CRM;

public class CrmActivityRepository : ICrmActivityRepository
{
    private readonly DomainDbContext _context;

    public CrmActivityRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<CrmActivity>> GetActivitiesAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId = null,
        Guid? leadId = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        bool? isCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CrmActivities
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.IsActive);

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (leadId.HasValue)
        {
            query = query.Where(x => x.LeadId == leadId.Value);
        }

        if (opportunityId.HasValue)
        {
            query = query.Where(x => x.OpportunityId == opportunityId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (isCompleted.HasValue)
        {
            query = query.Where(x => x.IsCompleted == isCompleted.Value);
        }

        return await query
            .OrderBy(x => x.DueDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CrmActivity?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid activityId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CrmActivities
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.CrmActivityId == activityId, cancellationToken);
    }

    public async Task AddAsync(CrmActivity activity, CancellationToken cancellationToken = default)
    {
        await _context.CrmActivities.AddAsync(activity, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
