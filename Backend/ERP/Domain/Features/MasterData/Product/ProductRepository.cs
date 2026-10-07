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
}