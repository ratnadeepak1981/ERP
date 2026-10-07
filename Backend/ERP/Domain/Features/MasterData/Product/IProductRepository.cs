namespace Domain.Features.MasterData.Product;

public interface IProductRepository
{
    Task<List<Product>> GetByTenantAsync(Guid tenantId);

    Task<Product?> GetByIdAsync(
        Guid tenantId,
        Guid productId);
}
