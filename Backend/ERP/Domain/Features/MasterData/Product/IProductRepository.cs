namespace Domain.Features.MasterData.Product;

public interface IProductRepository
{
    Task<List<Product>> GetByTenantAsync(Guid tenantId, Guid? brandId = null);

    Task<Product?> GetByIdAsync(
        Guid tenantId,
        Guid productId);

    Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string productCode);

    Task<bool> ExistsByNameAsync(
        Guid tenantId,
        string productName);

    Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId);

    Task<bool> BrandExistsAsync(Guid tenantId, Guid brandId);

    Task AddAsync(Product product);

    Task SaveChangesAsync();
}
