using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.PurchaseOrder;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly DomainDbContext _context;

    public PurchaseOrderRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseOrder>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId)
    {
        return await _context.PurchaseOrders
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive)
            .ToListAsync();
    }

    public async Task<List<PurchaseOrder>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        IEnumerable<Guid>? branchIds = null)
    {
        var query = _context.PurchaseOrders
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.IsActive);

        if (branchIds != null)
        {
            var branchList = branchIds.ToList();
            query = query.Where(x => branchList.Contains(x.BranchId));
        }

        return await query.ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        return await _context.PurchaseOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == orderId &&
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);
    }

    public async Task<PurchaseOrder?> GetByIdAndCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid orderId)
    {
        return await _context.PurchaseOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == orderId &&
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.IsActive);
    }

    public async Task<bool> ExistsOrderNumberAsync(
        Guid tenantId,
        Guid companyId,
        string orderNumber)
    {
        return await _context.PurchaseOrders
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.OrderNumber == orderNumber);
    }

    public async Task AddAsync(PurchaseOrder order)
    {
        await _context.PurchaseOrders.AddAsync(order);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
