using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;

namespace ERP.Application.Features.Inventory.Batch;

public class BatchService : IBatchService
{
    private readonly IInventoryPersistenceCoordinator _persistenceCoordinator;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public BatchService(
        IInventoryPersistenceCoordinator persistenceCoordinator,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository)
    {
        _persistenceCoordinator = persistenceCoordinator ?? throw new ArgumentNullException(nameof(persistenceCoordinator));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
    }

    public async Task<List<ProductBatchDto>> GetBatchesAsync(Guid tenantId, Guid? productId = null)
    {
        var batches = await _persistenceCoordinator.GetBatchesListAsync(tenantId, productId);
        if (batches.Count == 0) return new List<ProductBatchDto>();

        var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
        var products = await _persistenceCoordinator.GetActiveProductsByIdsAsync(tenantId, productIds);

        return batches.Select(b =>
        {
            products.TryGetValue(b.ProductId, out var product);
            return new ProductBatchDto
            {
                ProductBatchId = b.ProductBatchId,
                TenantId = b.TenantId,
                ProductId = b.ProductId,
                ProductCode = product?.ProductCode ?? string.Empty,
                ProductName = product?.ProductName ?? string.Empty,
                BatchNumber = b.BatchNumber,
                ManufacturingDate = b.ManufacturingDate,
                ExpiryDate = b.ExpiryDate,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt
            };
        }).ToList();
    }

    public async Task<ProductBatchDto?> GetBatchByIdAsync(Guid tenantId, Guid productBatchId)
    {
        var batch = await _persistenceCoordinator.GetBatchByIdAsync(tenantId, productBatchId);
        if (batch == null) return null;

        var product = await _productRepository.GetByIdAsync(tenantId, batch.ProductId);

        return new ProductBatchDto
        {
            ProductBatchId = batch.ProductBatchId,
            TenantId = batch.TenantId,
            ProductId = batch.ProductId,
            ProductCode = product?.ProductCode ?? string.Empty,
            ProductName = product?.ProductName ?? string.Empty,
            BatchNumber = batch.BatchNumber,
            ManufacturingDate = batch.ManufacturingDate,
            ExpiryDate = batch.ExpiryDate,
            IsActive = batch.IsActive,
            CreatedAt = batch.CreatedAt
        };
    }

    public async Task<ProductBatchDto?> GetBatchByNumberAsync(Guid tenantId, Guid productId, string batchNumber)
    {
        string normBatch = batchNumber?.Trim() ?? string.Empty;
        var batch = await _inventoryRepository.GetBatchAsync(tenantId, productId, normBatch);
        if (batch == null) return null;

        var product = await _productRepository.GetByIdAsync(tenantId, batch.ProductId);

        return new ProductBatchDto
        {
            ProductBatchId = batch.ProductBatchId,
            TenantId = batch.TenantId,
            ProductId = batch.ProductId,
            ProductCode = product?.ProductCode ?? string.Empty,
            ProductName = product?.ProductName ?? string.Empty,
            BatchNumber = batch.BatchNumber,
            ManufacturingDate = batch.ManufacturingDate,
            ExpiryDate = batch.ExpiryDate,
            IsActive = batch.IsActive,
            CreatedAt = batch.CreatedAt
        };
    }

    public async Task<ProductBatchDto> CreateBatchAsync(Guid tenantId, CreateProductBatchRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.ProductId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(request.ProductId));

        if (string.IsNullOrWhiteSpace(request.BatchNumber))
            throw new ArgumentException("BatchNumber is required.", nameof(request.BatchNumber));

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new KeyNotFoundException("Product not found or does not belong to the current tenant.");

        if (product.TrackingMode != TrackingMode.Batch && product.TrackingMode != TrackingMode.BatchAndSerial)
        {
            throw new InvalidOperationException($"Product '{product.ProductCode}' tracking mode is '{product.TrackingMode}', which does not track batches.");
        }

        string normBatch = request.BatchNumber.Trim();

        // 1. Cross-product mismatch check
        var batchInTenant = await _inventoryRepository.GetBatchByNumberAsync(tenantId, normBatch);
        if (batchInTenant != null && batchInTenant.ProductId != request.ProductId)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' is already associated with another product in this tenant. A batch number cannot be reused for a different product.");
        }

        // 2. Existing batch check
        var existing = await _inventoryRepository.GetBatchAsync(tenantId, request.ProductId, normBatch);

        if (existing != null)
        {
            if (request.ExpiryDate.HasValue && existing.ExpiryDate.HasValue &&
                request.ExpiryDate.Value.Date != existing.ExpiryDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Conflicting expiry date for existing batch '{normBatch}'. Registered: {existing.ExpiryDate.Value:yyyy-MM-dd}, Provided: {request.ExpiryDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
            }

            if (request.ManufacturingDate.HasValue && existing.ManufacturingDate.HasValue &&
                request.ManufacturingDate.Value.Date != existing.ManufacturingDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Conflicting manufacturing date for existing batch '{normBatch}'. Registered: {existing.ManufacturingDate.Value:yyyy-MM-dd}, Provided: {request.ManufacturingDate.Value:yyyy-MM-dd}. Existing batch dates cannot be altered.");
            }

            return new ProductBatchDto
            {
                ProductBatchId = existing.ProductBatchId,
                TenantId = existing.TenantId,
                ProductId = existing.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                BatchNumber = existing.BatchNumber,
                ManufacturingDate = existing.ManufacturingDate,
                ExpiryDate = existing.ExpiryDate,
                IsActive = existing.IsActive,
                CreatedAt = existing.CreatedAt
            };
        }

        var batch = ProductBatch.Create(
            tenantId,
            request.ProductId,
            normBatch,
            request.ManufacturingDate,
            request.ExpiryDate);

        await _persistenceCoordinator.AddProductBatchAsync(batch);
        await _persistenceCoordinator.SaveChangesAsync();

        return new ProductBatchDto
        {
            ProductBatchId = batch.ProductBatchId,
            TenantId = batch.TenantId,
            ProductId = batch.ProductId,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            BatchNumber = batch.BatchNumber,
            ManufacturingDate = batch.ManufacturingDate,
            ExpiryDate = batch.ExpiryDate,
            IsActive = batch.IsActive,
            CreatedAt = batch.CreatedAt
        };
    }

    public async Task<List<EligibleBatchDto>> GetEligibleBatchesAsync(
        Guid tenantId,
        Guid productId,
        Guid? warehouseId = null,
        Guid? locationId = null,
        DateTime? asOfDate = null)
    {
        return await _inventoryRepository.GetEligibleBatchesAsync(
            tenantId,
            productId,
            warehouseId,
            locationId,
            asOfDate);
    }
}
