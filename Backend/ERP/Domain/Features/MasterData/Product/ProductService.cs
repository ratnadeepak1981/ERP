using Domain.Features.MasterData.Product;

namespace Domain.Features.MasterData.Product;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Product>> GetProductsAsync(
        Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<Product?> GetProductAsync(
        Guid tenantId,
        Guid productId)
    {
        return await _repository.GetByIdAsync(
            tenantId,
            productId);
    }
}