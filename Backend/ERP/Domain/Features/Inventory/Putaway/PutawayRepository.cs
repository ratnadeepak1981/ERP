using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.Inventory.Putaway;

public class PutawayRepository : IPutawayRepository
{
    private readonly DomainDbContext _context;

    public PutawayRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<Putaway?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId)
    {
        return await _context.Putaways
            .Include(p => p.Lines)
                .ThenInclude(l => l.Allocations)
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.CompanyId == companyId &&
                p.BranchId == branchId &&
                p.PutawayId == putawayId);
    }

    public async Task<List<Putaway>> GetPutawaysAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid? warehouseId = null,
        PutawayStatus? status = null)
    {
        var query = _context.Putaways
            .Include(p => p.Lines)
                .ThenInclude(l => l.Allocations)
            .Where(p =>
                p.TenantId == tenantId &&
                p.CompanyId == companyId &&
                p.BranchId == branchId);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(p => p.WarehouseId == warehouseId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query
            .OrderByDescending(p => p.PutawayDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Putaway putaway)
    {
        await _context.Putaways.AddAsync(putaway);
    }

    public Task UpdateAsync(Putaway putaway)
    {
        _context.Putaways.Update(putaway);
        return Task.CompletedTask;
    }

    public async Task<Dictionary<Guid, decimal>> GetCompletedPutawayQuantitiesByReceiptLineAsync(Guid tenantId, Guid sourceDocId)
    {
        var allocations = await _context.PutawayLines
            .Where(l => _context.Putaways.Any(p =>
                p.PutawayId == l.PutawayId &&
                p.TenantId == tenantId &&
                p.SourceDocumentId == sourceDocId &&
                p.Status == PutawayStatus.Completed))
            .GroupBy(l => l.SourceDocumentLineId ?? Guid.Empty)
            .Select(g => new
            {
                SourceLineId = g.Key,
                TotalPutaway = g.Sum(x => x.PutawayQuantity)
            })
            .ToListAsync();

        return allocations.ToDictionary(x => x.SourceLineId, x => x.TotalPutaway);
    }

    public async Task<decimal> GetCompletedPutawayQuantityBySourceLineAsync(Guid tenantId, Guid sourceDocId, Guid sourceDocLineId)
    {
        return await _context.PutawayLines
            .Where(l => l.SourceDocumentLineId == sourceDocLineId &&
                        _context.Putaways.Any(p =>
                            p.PutawayId == l.PutawayId &&
                            p.TenantId == tenantId &&
                            p.SourceDocumentId == sourceDocId &&
                            p.Status == PutawayStatus.Completed))
            .SumAsync(l => (decimal?)l.PutawayQuantity) ?? 0m;
    }

    public async Task<decimal> GetCompletedPutawayQuantityByProductAndReceiptAsync(Guid tenantId, Guid sourceDocId, Guid productId)
    {
        return await _context.PutawayLines
            .Where(l => l.ProductId == productId &&
                        _context.Putaways.Any(p =>
                            p.PutawayId == l.PutawayId &&
                            p.TenantId == tenantId &&
                            p.SourceDocumentId == sourceDocId &&
                            p.Status == PutawayStatus.Completed))
            .SumAsync(l => (decimal?)l.PutawayQuantity) ?? 0m;
    }

    public async Task<string> GenerateNextPutawayNumberAsync(Guid tenantId, int year)
    {
        string prefix = $"PA-{year}-";
        var latest = await _context.Putaways
            .Where(p => p.TenantId == tenantId && p.PutawayNumber.StartsWith(prefix))
            .OrderByDescending(p => p.PutawayNumber)
            .Select(p => p.PutawayNumber)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(latest))
        {
            var parts = latest.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int parsed))
            {
                nextSeq = parsed + 1;
            }
        }

        return $"{prefix}{nextSeq:D4}";
    }
}
