using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.SalesQuotation;

public class SalesQuotationRepository : ISalesQuotationRepository
{
    private readonly DomainDbContext _context;

    public SalesQuotationRepository(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<SalesQuotation>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesQuotationStatus? status = null)
    {
        var query = _context.SalesQuotations
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

        return await query.OrderByDescending(x => x.QuotationDate).ToListAsync();
    }

    public async Task<SalesQuotation?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid quotationId)
    {
        return await _context.SalesQuotations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == quotationId &&
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.BranchId == branchId &&
                x.IsActive);
    }

    public async Task<SalesQuotation?> GetByIdUnscopedAsync(
        Guid tenantId,
        Guid quotationId)
    {
        return await _context.SalesQuotations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == quotationId &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<List<SalesQuotation>> GetByCustomerAsync(
        Guid tenantId,
        Guid customerId,
        SalesQuotationStatus? status = null)
    {
        var query = _context.SalesQuotations
            .Include(x => x.Items)
            .Where(x =>
                x.TenantId == tenantId &&
                x.CustomerId == customerId &&
                x.IsActive);

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return await query.OrderByDescending(x => x.QuotationDate).ToListAsync();
    }

    public async Task<bool> ExistsQuotationNumberAsync(
        Guid tenantId,
        Guid companyId,
        string quotationNumber)
    {
        return await _context.SalesQuotations
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.CompanyId == companyId &&
                x.QuotationNumber == quotationNumber &&
                x.IsActive);
    }

    public async Task<int> GetCountByCompanyAsync(
        Guid tenantId,
        Guid companyId)
    {
        return await _context.SalesQuotations
            .CountAsync(x => x.TenantId == tenantId && x.CompanyId == companyId);
    }

    public async Task AddAsync(SalesQuotation quotation)
    {
        await _context.SalesQuotations.AddAsync(quotation);
    }

    public async Task AddItemAsync(SalesQuotationItem item)
    {
        await _context.SalesQuotationItems.AddAsync(item);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
