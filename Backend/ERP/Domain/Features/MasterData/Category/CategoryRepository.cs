using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.MasterData.Category;

public class CategoryRepository : ICategoryRepository
{
    private readonly DomainDbContext _context;

    public CategoryRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetByTenantAsync(Guid tenantId)
    {
        return await _context.Categories
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(Guid tenantId, Guid categoryId)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CategoryId == categoryId && x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string categoryCode)
    {
        return await _context.Categories
            .AnyAsync(x => x.TenantId == tenantId && x.CategoryCode == categoryCode);
    }

    public async Task<bool> ExistsByNameAsync(Guid tenantId, string categoryName)
    {
        return await _context.Categories
            .AnyAsync(x => x.TenantId == tenantId && x.CategoryName == categoryName);
    }

    public async Task AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
