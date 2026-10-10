using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domain.Features.MasterData.Product;

public class ProductRepository : IProductRepository
{
    private readonly DomainDbContext _context;

    public ProductRepository(DomainDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetByTenantAsync(
        Guid tenantId)
    {
        return await _context.Products
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(
        Guid tenantId,
        Guid productId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(x =>
                x.ProductId == productId &&
                x.TenantId == tenantId &&
                x.IsActive);
    }

    public async Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string productCode)
    {
        return await _context.Products
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductCode == productCode);
    }

    public async Task<bool> ExistsByNameAsync(
        Guid tenantId,
        string productName)
    {
        return await _context.Products
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductName == productName);
    }

    public async Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId)
    {
        return await _context.Categories
            .AnyAsync(x => x.TenantId == tenantId && x.CategoryId == categoryId && x.IsActive);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}