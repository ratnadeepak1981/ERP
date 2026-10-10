using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using SaaS.Application.Interfaces;

namespace Domain.Features.MasterData.Product;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly ISubscriptionUsageService? _subscriptionUsageService;

    public ProductService(
        IProductRepository repository,
        IUnitOfMeasureRepository? uomRepository,
        ISubscriptionUsageService? subscriptionUsageService = null)
    {
        _repository = repository;
        _uomRepository = uomRepository!;
        _subscriptionUsageService = subscriptionUsageService;
    }

    public ProductService(
        IProductRepository repository,
        ISubscriptionUsageService? subscriptionUsageService)
        : this(repository, null, subscriptionUsageService)
    {
    }

    public async Task<List<Product>> GetProductsAsync(Guid tenantId)
    {
        return await _repository.GetByTenantAsync(tenantId);
    }

    public async Task<Product?> GetProductAsync(Guid tenantId, Guid productId)
    {
        return await _repository.GetByIdAsync(tenantId, productId);
    }

    public async Task<Product> CreateProductAsync(Guid tenantId, CreateProductRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProductCode))
            throw new ArgumentException("Product code is required.", nameof(request.ProductCode));

        if (string.IsNullOrWhiteSpace(request.ProductName))
            throw new ArgumentException("Product name is required.", nameof(request.ProductName));

        string code = request.ProductCode.Trim();
        string name = request.ProductName.Trim();

        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Product code '{code}' already exists for this tenant.");

        if (await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Product name '{name}' already exists for this tenant.");

        // Category tenant isolation check
        if (!await _repository.CategoryExistsAsync(tenantId, request.CategoryId))
            throw new InvalidOperationException("Category not found or does not belong to the current tenant.");

        // UOM resolution and validation
        Guid? resolvedUomId = null;
        string resolvedUomCode = string.IsNullOrWhiteSpace(request.UnitOfMeasure) ? "PCS" : request.UnitOfMeasure.Trim();

        if (_uomRepository != null)
        {
            if (request.UnitOfMeasureId.HasValue)
            {
                var uom = await _uomRepository.GetUnitByIdAsync(tenantId, request.UnitOfMeasureId.Value);
                if (uom == null)
                    throw new InvalidOperationException("Unit of measure not found or does not belong to the current tenant.");
                resolvedUomId = uom.UnitOfMeasureId;
                resolvedUomCode = uom.Code;
            }
            else
            {
                var uom = await _uomRepository.GetUnitByCodeAsync(tenantId, resolvedUomCode);
                if (uom != null)
                {
                    resolvedUomId = uom.UnitOfMeasureId;
                    resolvedUomCode = uom.Code;
                }
                else
                {
                    // Reject unknown string-based UOM
                    throw new InvalidOperationException($"Unit of measure '{resolvedUomCode}' does not exist for this tenant.");
                }
            }
        }

        var product = Product.Create(
            tenantId,
            code,
            name,
            request.CategoryId,
            resolvedUomCode,
            request.Description?.Trim(),
            request.ValuationMethod,
            request.StandardCost,
            request.SellingPrice,
            request.ReorderLevel,
            request.ReorderQuantity,
            request.IsManufacturable,
            request.IsPurchasable,
            request.IsSellable,
            null,
            request.CanConsumeInProduction,
            request.CanConsumeInMaintenance,
            request.IsStockTracked,
            request.ProductType,
            request.TrackingMode,
            resolvedUomId);

        if (_subscriptionUsageService != null)
        {
            return await _subscriptionUsageService.ExecuteWithUsageLimitAsync(
                tenantId,
                "MASTER_DATA",
                1m,
                async () =>
                {
                    await _repository.AddAsync(product);
                    await _repository.SaveChangesAsync();
                    return product;
                });
        }

        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();

        return product;
    }

    public async Task<Product> UpdateProductAsync(Guid tenantId, Guid productId, UpdateProductRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProductName))
            throw new ArgumentException("Product name is required.", nameof(request.ProductName));

        var product = await _repository.GetByIdAsync(tenantId, productId)
            ?? throw new KeyNotFoundException("Product not found.");

        string name = request.ProductName.Trim();
        if (product.ProductName != name && await _repository.ExistsByNameAsync(tenantId, name))
            throw new InvalidOperationException($"Product name '{name}' already exists for this tenant.");

        if (!await _repository.CategoryExistsAsync(tenantId, request.CategoryId))
            throw new InvalidOperationException("Category not found or does not belong to the current tenant.");

        Guid? resolvedUomId = null;
        string resolvedUomCode = string.IsNullOrWhiteSpace(request.UnitOfMeasure) ? "PCS" : request.UnitOfMeasure.Trim();

        if (request.UnitOfMeasureId.HasValue)
        {
            var uom = await _uomRepository.GetUnitByIdAsync(tenantId, request.UnitOfMeasureId.Value);
            if (uom == null)
                throw new InvalidOperationException("Unit of measure not found or does not belong to the current tenant.");
            resolvedUomId = uom.UnitOfMeasureId;
            resolvedUomCode = uom.Code;
        }
        else
        {
            var uom = await _uomRepository.GetUnitByCodeAsync(tenantId, resolvedUomCode);
            if (uom != null)
            {
                resolvedUomId = uom.UnitOfMeasureId;
                resolvedUomCode = uom.Code;
            }
            else
            {
                throw new InvalidOperationException($"Unit of measure '{resolvedUomCode}' does not exist for this tenant.");
            }
        }

        product.Update(
            name,
            request.CategoryId,
            resolvedUomCode,
            request.Description?.Trim(),
            request.ValuationMethod,
            request.StandardCost,
            request.SellingPrice,
            request.ReorderLevel,
            request.ReorderQuantity,
            request.IsManufacturable,
            request.IsPurchasable,
            request.IsSellable,
            request.CanConsumeInProduction,
            request.CanConsumeInMaintenance,
            request.IsStockTracked,
            request.ProductType,
            request.TrackingMode,
            resolvedUomId);

        await _repository.SaveChangesAsync();
        return product;
    }

    public async Task DeactivateProductAsync(Guid tenantId, Guid productId)
    {
        var product = await _repository.GetByIdAsync(tenantId, productId)
            ?? throw new KeyNotFoundException("Product not found.");

        product.Deactivate();
        await _repository.SaveChangesAsync();
    }
}