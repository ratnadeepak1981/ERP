using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public class GoodsReceiptNoteRepository : IGoodsReceiptNoteRepository
{
    private readonly DomainDbContext _context;

    public GoodsReceiptNoteRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<GoodsReceiptNote>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId)
    {
        return await _context.GoodsReceiptNotes
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.CompanyId == companyId && x.BranchId == branchId)
            .OrderByDescending(x => x.ReceiptDate)
            .ToListAsync();
    }

    public async Task<GoodsReceiptNote?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId)
    {
        return await _context.GoodsReceiptNotes
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.BranchId == branchId && x.GoodsReceiptNoteId == grnId);
    }

    public async Task<GoodsReceiptNote?> GetByIdWithLinesAsync(Guid tenantId, Guid grnId)
    {
        return await _context.GoodsReceiptNotes
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.GoodsReceiptNoteId == grnId);
    }

    public async Task<bool> ExistsGrnNumberAsync(Guid tenantId, Guid companyId, string grnNumber)
    {
        return await _context.GoodsReceiptNotes
            .AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId && x.GrnNumber == grnNumber);
    }

    public async Task<List<GoodsReceiptNote>> GetByPurchaseOrderIdAsync(Guid tenantId, Guid purchaseOrderId)
    {
        return await _context.GoodsReceiptNotes
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();
    }

    public async Task AddAsync(GoodsReceiptNote grn)
    {
        await _context.GoodsReceiptNotes.AddAsync(grn);
    }

    public Task DeleteDraftAsync(GoodsReceiptNote grn)
    {
        _context.GoodsReceiptNotes.Remove(grn);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
