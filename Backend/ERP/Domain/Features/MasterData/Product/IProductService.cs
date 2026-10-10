namespace Domain.Features.MasterData.Product;

public interface IProductService
{
    Task<List<Product>> GetProductsAsync(Guid tenantId);

    Task<Product?> GetProductAsync(
        Guid tenantId,
        Guid productId);

    Task<Product> CreateProductAsync(
        Guid tenantId,
        CreateProductRequest request);

    Task<Product> UpdateProductAsync(
        Guid tenantId,
        Guid productId,
        UpdateProductRequest request);

    Task DeactivateProductAsync(
        Guid tenantId,
        Guid productId);
}