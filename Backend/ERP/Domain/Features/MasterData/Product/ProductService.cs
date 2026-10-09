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

    public async Task<Product> CreateProductAsync(
        Guid tenantId,
        CreateProductRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ProductCode))
        {
            throw new ArgumentException(
                "Product code is required.",
                nameof(request.ProductCode));
        }

        if (string.IsNullOrWhiteSpace(request.ProductName))
        {
            throw new ArgumentException(
                "Product name is required.",
                nameof(request.ProductName));
        }

        string code = request.ProductCode.Trim();
        string name = request.ProductName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
        {
            throw new InvalidOperationException(
                $"Product code '{code}' already exists for this tenant.");
        }

        if (await _repository.ExistsByNameAsync(tenantId, name))
        {
            throw new InvalidOperationException(
                $"Product name '{name}' already exists for this tenant.");
        }

        var product = Product.Create(
            tenantId,
            code,
            name,
            request.CategoryId,
            string.IsNullOrWhiteSpace(request.UnitOfMeasure) ? "PCS" : request.UnitOfMeasure.Trim(),
            request.Description,
            request.ValuationMethod,
            request.StandardCost,
            request.SellingPrice,
            request.ReorderLevel,
            request.ReorderQuantity,
            request.IsManufacturable,
            request.IsPurchasable,
            request.IsSellable);

        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();

        return product;
    }
}