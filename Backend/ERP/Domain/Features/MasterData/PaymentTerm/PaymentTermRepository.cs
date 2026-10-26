using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.PaymentTerm;

public class PaymentTermRepository : IPaymentTermRepository
{
    private readonly DomainDbContext _context;

    public PaymentTermRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<PaymentTerm>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Set<PaymentTerm>()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Days)
            .ThenBy(p => p.TermCode)
            .ToListAsync();
    }

    public async Task<PaymentTerm?> GetByIdAsync(Guid tenantId, Guid paymentTermId)
    {
        return await _context.Set<PaymentTerm>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.PaymentTermId == paymentTermId);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string termCode)
    {
        return await _context.Set<PaymentTerm>()
            .AnyAsync(p => p.TenantId == tenantId && p.TermCode == termCode);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string termName)
    {
        return await _context.Set<PaymentTerm>()
            .AnyAsync(p => p.TenantId == tenantId && p.TermName == termName);
    }

    public async Task AddAsync(PaymentTerm paymentTerm)
    {
        await _context.Set<PaymentTerm>().AddAsync(paymentTerm);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
