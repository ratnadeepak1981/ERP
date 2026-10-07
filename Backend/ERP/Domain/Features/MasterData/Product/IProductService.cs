namespace Domain.Features.MasterData.Product;

public interface IProductService
{
    Task<List<Product>> GetProductsAsync(Guid tenantId);

    Task<Product?> GetProductAsync(
        Guid tenantId,
        Guid productId);
}