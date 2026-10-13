using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.SalesOrder;

public class SalesOrderRepository : ISalesOrderRepository
{
    private readonly DomainDbContext _context;

    public SalesOrderRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalesOrder>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesOrderStatus? status = null)
    {
        var query = _context.SalesOrders
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<SalesOrder?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId)
    {
        return await _context.SalesOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == orderId &&
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);
    }

    public async Task<bool> ExistsOrderNumberAsync(
        Guid tenantId,
        Guid companyId,
        string orderNumber)
    {
        return await _context.SalesOrders
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.OrderNumber == orderNumber);
    }

    public async Task AddAsync(SalesOrder order)
    {
        await _context.SalesOrders.AddAsync(order);
    }

    public async Task AddItemAsync(SalesOrderItem item)
    {
        await _context.SalesOrderItems.AddAsync(item);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
