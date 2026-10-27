using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory.Batch;

public class BatchService : IBatchService
{
    private readonly DomainDbContext _context;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public BatchService(
        DomainDbContext context,
        IProductRepository productRepository,
        IInventoryRepository? inventoryRepository = null)
    {
        _context = context;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository ?? new InventoryRepository(context);
    }

    public async Task<List<ProductBatchDto>> GetBatchesAsync(Guid tenantId, Guid? productId = null)
    {
        var query = _context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId);

        if (productId.HasValue && productId.Value != Guid.Empty)
        {
            query = query.Where(b => b.ProductId == productId.Value);
        }

        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId);

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
        var batch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductBatchId == productBatchId);

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
        var batch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == productId && b.BatchNumber == normBatch);

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
        var otherProductBatch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.BatchNumber == normBatch && b.ProductId != request.ProductId);

        if (otherProductBatch != null)
        {
            throw new InvalidOperationException(
                $"Batch '{normBatch}' is already associated with another product in this tenant. A batch number cannot be reused for a different product.");
        }

        // 2. Existing batch check
        var existing = await _context.ProductBatches
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == request.ProductId && b.BatchNumber == normBatch);

        if (existing != null)
        {
            // Check conflicting dates
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

        await _context.ProductBatches.AddAsync(batch);
        await _context.SaveChangesAsync();

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
