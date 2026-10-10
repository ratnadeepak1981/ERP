using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.PurchaseRequisition;

public class PurchaseRequisitionRepository : IPurchaseRequisitionRepository
{
    private readonly DomainDbContext _context;

    public PurchaseRequisitionRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseRequisition>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        return await _context.PurchaseRequisitions
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.BranchId == branchId && x.IsActive)
            .ToListAsync();
    }

    public async Task<PurchaseRequisition?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid requisitionId)
    {
        return await _context.PurchaseRequisitions
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.BranchId == branchId && x.Id == requisitionId);
    }

    public async Task<bool> ExistsRequisitionNumberAsync(Guid tenantId, Guid companyId, string requisitionNumber)
    {
        return await _context.PurchaseRequisitions
            .AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.RequisitionNumber == requisitionNumber);
    }

    public async Task AddAsync(PurchaseRequisition requisition)
    {
        await _context.PurchaseRequisitions.AddAsync(requisition);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
