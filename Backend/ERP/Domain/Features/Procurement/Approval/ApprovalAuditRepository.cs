using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.Approval;

public class ApprovalAuditRepository : IApprovalAuditRepository
{
    private readonly DomainDbContext _context;

    public ApprovalAuditRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task AddRecordAsync(ApprovalAuditRecord record)
    {
        await _context.ApprovalAuditRecords.AddAsync(record);
    }

    public async Task<List<ApprovalAuditRecord>> GetRecordsAsync(Guid tenantId, string documentType, Guid documentId)
    {
        return await _context.ApprovalAuditRecords
            .Where(x => x.TenantId == tenantId && x.DocumentType == documentType && x.DocumentId == documentId)
            .OrderBy(x => x.DecisionDateUtc)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
