using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Sales.Invoice;

public class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly DomainDbContext _context;

    public SalesInvoiceRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalesInvoice>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesInvoiceStatus? status = null)
    {
        var query = _context.SalesInvoices
            .Include(i => i.Items)
            .Where(i => i.TenantId == tenantId && i.CompanyId == companyId && i.BranchId == branchId && i.IsActive);

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt).ToListAsync();
    }

    public async Task<List<SalesInvoice>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        SalesInvoiceStatus? status = null)
    {
        var query = _context.SalesInvoices
            .Include(i => i.Items)
            .Where(i => i.TenantId == tenantId && i.CompanyId == companyId && i.IsActive);

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.CreatedAt).ToListAsync();
    }

    public async Task<SalesInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid invoiceId)
    {
        return await _context.SalesInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.CompanyId == companyId && i.BranchId == branchId && i.Id == invoiceId && i.IsActive);
    }

    public async Task<SalesInvoice?> GetByIdAsync(
        Guid tenantId,
        Guid invoiceId)
    {
        return await _context.SalesInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId && i.IsActive);
    }

    public async Task<SalesInvoice?> GetByShipmentIdAsync(
        Guid tenantId,
        Guid shipmentId)
    {
        return await _context.SalesInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.SalesShipmentId == shipmentId && i.IsActive);
    }

    public async Task<bool> ExistsInvoiceNumberAsync(
        Guid tenantId,
        Guid companyId,
        string invoiceNumber)
    {
        string norm = invoiceNumber.Trim().ToLower();
        return await _context.SalesInvoices
            .AnyAsync(i => i.TenantId == tenantId && i.CompanyId == companyId && i.InvoiceNumber.ToLower() == norm && i.IsActive);
    }

    public async Task AddAsync(SalesInvoice invoice)
    {
        await _context.SalesInvoices.AddAsync(invoice);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
