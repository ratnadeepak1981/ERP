using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Ecommerce;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Features.Ecommerce;

public class EcommerceProductService : IEcommerceProductService
{
    private readonly DomainDbContext _context;
    private readonly ILogger<EcommerceProductService> _logger;

    public EcommerceProductService(
        DomainDbContext context,
        ILogger<EcommerceProductService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<EcommerceProductPublicationDto>> GetCompanyPublicationsAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var company = await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive, ct);

        if (company == null)
            throw new ArgumentException("Selling company not found or inactive within tenant scope.");

        string currency = company.BaseCurrency?.CurrencyCode ?? "USD";

        var publications = await _context.EcommerceProducts
            .AsNoTracking()
            .Include(ep => ep.Product)
            .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId)
            .OrderBy(ep => ep.DisplayOrder)
            .ThenBy(ep => ep.CreatedAtUtc)
            .ToListAsync(ct);

        // Preload categories and brands
        var categoryIds = publications
            .Where(ep => ep.Product != null)
            .Select(ep => ep.Product!.CategoryId)
            .Distinct()
            .ToList();

        var brandIds = publications
            .Where(ep => ep.Product != null && ep.Product!.BrandId.HasValue)
            .Select(ep => ep.Product!.BrandId!.Value)
            .Distinct()
            .ToList();

        var categories = await _context.Categories
            .Where(c => c.TenantId == tenantId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.CategoryName, ct);

        var brands = await _context.Brands
            .Where(b => b.TenantId == tenantId && brandIds.Contains(b.BrandId))
            .ToDictionaryAsync(b => b.BrandId, b => b.BrandName, ct);

        return publications.Select(ep =>
        {
            var p = ep.Product;
            string? catName = null;
            string? brandName = null;

            if (p != null)
            {
                categories.TryGetValue(p.CategoryId, out catName);
                if (p.BrandId.HasValue)
                    brands.TryGetValue(p.BrandId.Value, out brandName);
            }

            return new EcommerceProductPublicationDto
            {
                Id = ep.Id,
                TenantId = ep.TenantId,
                CompanyId = ep.CompanyId,
                ProductId = ep.ProductId,
                ProductCode = p?.ProductCode ?? string.Empty,
                ProductName = p?.ProductName ?? string.Empty,
                IsPublished = ep.IsPublished,
                OnlineDescription = ep.OnlineDescription,
                DisplayOrder = ep.DisplayOrder,
                IsSellable = p?.IsSellable ?? false,
                IsProductActive = p?.IsActive ?? false,
                SellingPrice = p?.SellingPrice ?? 0,
                Currency = currency,
                CategoryId = p?.CategoryId ?? Guid.Empty,
                CategoryName = catName,
                BrandId = p?.BrandId,
                BrandName = brandName,
                UnitOfMeasure = p?.UnitOfMeasure ?? string.Empty,
                CreatedAtUtc = ep.CreatedAtUtc,
                UpdatedAtUtc = ep.UpdatedAtUtc
            };
        }).ToList();
    }

    public async Task<EcommerceProductPublicationDto> GetPublicationByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var company = await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive, ct);

        if (company == null)
            throw new ArgumentException("Selling company not found or inactive within tenant scope.");

        string currency = company.BaseCurrency?.CurrencyCode ?? "USD";

        var ep = await _context.EcommerceProducts
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.CompanyId == companyId && p.Id == publicationId, ct);

        if (ep == null)
            throw new KeyNotFoundException($"Publication record '{publicationId}' not found for company '{companyId}'.");

        var p = ep.Product;
        string? catName = null;
        string? brandName = null;

        if (p != null)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CategoryId == p.CategoryId, ct);
            catName = cat?.CategoryName;

            if (p.BrandId.HasValue)
            {
                var brand = await _context.Brands.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.BrandId == p.BrandId.Value, ct);
                brandName = brand?.BrandName;
            }
        }

        return new EcommerceProductPublicationDto
        {
            Id = ep.Id,
            TenantId = ep.TenantId,
            CompanyId = ep.CompanyId,
            ProductId = ep.ProductId,
            ProductCode = p?.ProductCode ?? string.Empty,
            ProductName = p?.ProductName ?? string.Empty,
            IsPublished = ep.IsPublished,
            OnlineDescription = ep.OnlineDescription,
            DisplayOrder = ep.DisplayOrder,
            IsSellable = p?.IsSellable ?? false,
            IsProductActive = p?.IsActive ?? false,
            SellingPrice = p?.SellingPrice ?? 0,
            Currency = currency,
            CategoryId = p?.CategoryId ?? Guid.Empty,
            CategoryName = catName,
            BrandId = p?.BrandId,
            BrandName = brandName,
            UnitOfMeasure = p?.UnitOfMeasure ?? string.Empty,
            CreatedAtUtc = ep.CreatedAtUtc,
            UpdatedAtUtc = ep.UpdatedAtUtc
        };
    }

    public async Task<EcommerceProductPublicationDto> CreatePublicationAsync(
        Guid tenantId,
        Guid companyId,
        CreateEcommerceProductRequest request,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.ProductId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(request.ProductId));

        // 1. Verify Company exists and belongs to Tenant
        var company = await _context.Companies
            .Include(c => c.BaseCurrency)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive, ct);

        if (company == null)
            throw new ArgumentException("Selling company not found or inactive within tenant scope.");

        // 2. Verify Product exists and belongs to Tenant
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == request.ProductId, ct);

        if (product == null)
            throw new ArgumentException($"Product '{request.ProductId}' not found within tenant scope.");

        // Rule 3: Only active and sellable ERP products may be published
        if (!product.IsActive)
            throw new InvalidOperationException("Cannot publish inactive product. Only active ERP products can be published.");

        if (!product.IsSellable)
            throw new InvalidOperationException("Cannot publish non-sellable product. Product must have sellable capability enabled.");

        // Rule: Unique publication record per Tenant + Company + Product
        bool exists = await _context.EcommerceProducts
            .AnyAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.ProductId == request.ProductId, ct);

        if (exists)
            throw new InvalidOperationException($"Product '{product.ProductCode}' is already registered for publication in this selling company.");

        var publication = EcommerceProduct.Create(
            tenantId,
            companyId,
            request.ProductId,
            request.OnlineDescription,
            request.DisplayOrder,
            request.IsPublished);

        await _context.EcommerceProducts.AddAsync(publication, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created e-commerce publication {PublicationId} for product {ProductCode} in company {CompanyId}",
            publication.Id, product.ProductCode, companyId);

        return await GetPublicationByIdAsync(tenantId, companyId, publication.Id, ct);
    }

    public async Task<EcommerceProductPublicationDto> UpdatePublicationAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        UpdateEcommerceProductRequest request,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var publication = await _context.EcommerceProducts
            .FirstOrDefaultAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.Id == publicationId, ct);

        if (publication == null)
            throw new KeyNotFoundException($"Publication record '{publicationId}' not found for company '{companyId}'.");

        publication.UpdateDetails(request.OnlineDescription, request.DisplayOrder);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated e-commerce publication details for {PublicationId}", publicationId);

        return await GetPublicationByIdAsync(tenantId, companyId, publication.Id, ct);
    }

    public async Task<EcommerceProductPublicationDto> PublishAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var publication = await _context.EcommerceProducts
            .Include(ep => ep.Product)
            .FirstOrDefaultAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.Id == publicationId, ct);

        if (publication == null)
            throw new KeyNotFoundException($"Publication record '{publicationId}' not found for company '{companyId}'.");

        // Verify product eligibility at time of publishing
        var product = publication.Product ?? await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProductId == publication.ProductId, ct);

        if (product == null || !product.IsActive)
            throw new InvalidOperationException("Cannot publish product because Product Master record is inactive or missing.");

        if (!product.IsSellable)
            throw new InvalidOperationException("Cannot publish product because Product Master record is marked non-sellable.");

        publication.Publish();
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Published e-commerce product publication {PublicationId} for company {CompanyId}", publicationId, companyId);

        return await GetPublicationByIdAsync(tenantId, companyId, publication.Id, ct);
    }

    public async Task<EcommerceProductPublicationDto> UnpublishAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var publication = await _context.EcommerceProducts
            .FirstOrDefaultAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.Id == publicationId, ct);

        if (publication == null)
            throw new KeyNotFoundException($"Publication record '{publicationId}' not found for company '{companyId}'.");

        // Rule 5: Unpublishing must not deactivate or delete the ERP Product Master record
        publication.Unpublish();
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Unpublished e-commerce product publication {PublicationId} for company {CompanyId}", publicationId, companyId);

        return await GetPublicationByIdAsync(tenantId, companyId, publication.Id, ct);
    }

    public async Task<bool> DeletePublicationAsync(
        Guid tenantId,
        Guid companyId,
        Guid publicationId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var publication = await _context.EcommerceProducts
            .FirstOrDefaultAsync(ep => ep.TenantId == tenantId && ep.CompanyId == companyId && ep.Id == publicationId, ct);

        if (publication == null)
            throw new KeyNotFoundException($"Publication record '{publicationId}' not found for company '{companyId}'.");

        // Rule 8: Deleting a publication does not delete or modify the Product Master record
        _context.EcommerceProducts.Remove(publication);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted e-commerce publication {PublicationId} for company {CompanyId}", publicationId, companyId);

        return true;
    }

    public async Task<int> BackfillCompanyPublicationsAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default)
    {
        ValidateTenantAndCompany(tenantId, companyId);

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId && c.IsActive, ct);

        if (company == null)
            throw new ArgumentException("Selling company not found or inactive within tenant scope.");

        var candidateProducts = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsSellable)
            .OrderBy(p => p.ProductCode)
            .ToListAsync(ct);

        if (!candidateProducts.Any())
            return 0;

        var existingProductIds = await _context.EcommerceProducts
            .AsNoTracking()
            .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId)
            .Select(ep => ep.ProductId)
            .ToListAsync(ct);

        var missing = candidateProducts.Where(p => !existingProductIds.Contains(p.ProductId)).ToList();
        if (!missing.Any())
            return 0;

        int nextOrder = existingProductIds.Count + 1;
        foreach (var p in missing)
        {
            var ep = EcommerceProduct.Create(
                tenantId,
                companyId,
                p.ProductId,
                p.Description,
                displayOrder: nextOrder++,
                isPublished: true);

            await _context.EcommerceProducts.AddAsync(ep, ct);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Backfilled {Count} product publication records for company {CompanyId} in tenant {TenantId}",
            missing.Count, companyId, tenantId);

        return missing.Count;
    }

    private async Task AutoBackfillMissingAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        try
        {
            var candidateProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && p.IsSellable)
                .Select(p => new { p.ProductId, p.Description })
                .ToListAsync(ct);

            if (!candidateProducts.Any())
                return;

            var existingProductIds = await _context.EcommerceProducts
                .AsNoTracking()
                .Where(ep => ep.TenantId == tenantId && ep.CompanyId == companyId)
                .Select(ep => ep.ProductId)
                .ToListAsync(ct);

            var missing = candidateProducts.Where(p => !existingProductIds.Contains(p.ProductId)).ToList();
            if (!missing.Any())
                return;

            int nextOrder = existingProductIds.Count + 1;
            foreach (var m in missing)
            {
                var ep = EcommerceProduct.Create(
                    tenantId,
                    companyId,
                    m.ProductId,
                    m.Description,
                    displayOrder: nextOrder++,
                    isPublished: true);

                await _context.EcommerceProducts.AddAsync(ep, ct);
            }

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoBackfill skipped due to concurrent execution or read-only context.");
        }
    }

    public async Task<IReadOnlyList<PublicEcommerceProductDto>> GetPublicCatalogueAsync(
        Guid companyId,
        PublicCatalogueFilter? filter = null,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));

        var companyQuery = _context.Companies
            .Include(c => c.BaseCurrency)
            .AsNoTracking()
            .Where(c => (c.Id == companyId || c.TenantId == companyId) && c.IsActive);

        if (tenantId.HasValue)
        {
            companyQuery = companyQuery.Where(c => c.TenantId == tenantId.Value);
        }

        var company = await companyQuery.FirstOrDefaultAsync(ct);
        if (company == null)
            throw new KeyNotFoundException("Selling company not found or inactive.");

        Guid resolvedCompanyId = company.Id;
        string currency = company.BaseCurrency?.CurrencyCode ?? "USD";

        // Auto-backfill active sellable products that lack publication records
        await AutoBackfillMissingAsync(company.TenantId, resolvedCompanyId, ct);

        // Query published products where ERP product is currently active and sellable
        var query = _context.EcommerceProducts
            .AsNoTracking()
            .Include(ep => ep.Product)
            .Where(ep => ep.CompanyId == resolvedCompanyId && ep.IsPublished && ep.Product != null && ep.Product.IsActive && ep.Product.IsSellable);

        if (tenantId.HasValue)
        {
            query = query.Where(ep => ep.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter?.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(ep =>
                ep.Product!.ProductName.ToLower().Contains(term) ||
                ep.Product.ProductCode.ToLower().Contains(term) ||
                (ep.OnlineDescription != null && ep.OnlineDescription.ToLower().Contains(term)));
        }

        if (filter?.CategoryId.HasValue == true && filter.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(ep => ep.Product!.CategoryId == filter.CategoryId.Value);
        }

        if (filter?.BrandId.HasValue == true && filter.BrandId.Value != Guid.Empty)
        {
            query = query.Where(ep => ep.Product!.BrandId == filter.BrandId.Value);
        }

        var list = await query
            .OrderBy(ep => ep.DisplayOrder)
            .ThenBy(ep => ep.Product!.ProductName)
            .ToListAsync(ct);

        // Preload category and brand names
        var categoryIds = list.Select(ep => ep.Product!.CategoryId).Distinct().ToList();
        var brandIds = list.Where(ep => ep.Product!.BrandId.HasValue).Select(ep => ep.Product!.BrandId!.Value).Distinct().ToList();

        var categories = await _context.Categories
            .Where(c => c.TenantId == company.TenantId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.CategoryName, ct);

        var brands = await _context.Brands
            .Where(b => b.TenantId == company.TenantId && brandIds.Contains(b.BrandId))
            .ToDictionaryAsync(b => b.BrandId, b => b.BrandName, ct);

        return list.Select(ep =>
        {
            var p = ep.Product!;
            categories.TryGetValue(p.CategoryId, out var catName);
            string? brandName = null;
            if (p.BrandId.HasValue)
                brands.TryGetValue(p.BrandId.Value, out brandName);

            // Rule 10: Never expose StandardCost, supplier costs, internal margins
            return new PublicEcommerceProductDto
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                OnlineDescription = !string.IsNullOrWhiteSpace(ep.OnlineDescription) ? ep.OnlineDescription : p.Description,
                DisplayOrder = ep.DisplayOrder,
                CategoryId = p.CategoryId,
                CategoryName = catName,
                BrandId = p.BrandId,
                BrandName = brandName,
                UnitOfMeasure = p.UnitOfMeasure,
                SellingPrice = p.SellingPrice,
                Currency = currency
            };
        }).ToList();
    }

    public async Task<PublicEcommerceProductDto> GetPublicProductDetailAsync(
        Guid companyId,
        Guid productId,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId is required.", nameof(productId));

        var companyQuery = _context.Companies
            .Include(c => c.BaseCurrency)
            .AsNoTracking()
            .Where(c => (c.Id == companyId || c.TenantId == companyId) && c.IsActive);

        if (tenantId.HasValue)
        {
            companyQuery = companyQuery.Where(c => c.TenantId == tenantId.Value);
        }

        var company = await companyQuery.FirstOrDefaultAsync(ct);
        if (company == null)
            throw new KeyNotFoundException("Selling company not found or inactive.");

        Guid resolvedCompanyId = company.Id;
        string currency = company.BaseCurrency?.CurrencyCode ?? "USD";

        var query = _context.EcommerceProducts
            .AsNoTracking()
            .Include(ep => ep.Product)
            .Where(ep => ep.CompanyId == resolvedCompanyId && ep.ProductId == productId && ep.IsPublished && ep.Product != null && ep.Product.IsActive && ep.Product.IsSellable);

        if (tenantId.HasValue)
        {
            query = query.Where(ep => ep.TenantId == tenantId.Value);
        }

        var ep = await query.FirstOrDefaultAsync(ct);
        if (ep == null || ep.Product == null)
            throw new KeyNotFoundException($"Product '{productId}' is not published or available in the public catalogue of this selling company.");

        var p = ep.Product;
        string? catName = null;
        var cat = await _context.Categories.FirstOrDefaultAsync(c => c.TenantId == company.TenantId && c.CategoryId == p.CategoryId, ct);
        catName = cat?.CategoryName;

        string? brandName = null;
        if (p.BrandId.HasValue)
        {
            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.TenantId == company.TenantId && b.BrandId == p.BrandId.Value, ct);
            brandName = brand?.BrandName;
        }

        // Rule 10: Never expose StandardCost or internal cost/margin data
        return new PublicEcommerceProductDto
        {
            ProductId = p.ProductId,
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            OnlineDescription = !string.IsNullOrWhiteSpace(ep.OnlineDescription) ? ep.OnlineDescription : p.Description,
            DisplayOrder = ep.DisplayOrder,
            CategoryId = p.CategoryId,
            CategoryName = catName,
            BrandId = p.BrandId,
            BrandName = brandName,
            UnitOfMeasure = p.UnitOfMeasure,
            SellingPrice = p.SellingPrice,
            Currency = currency
        };
    }

    private static void ValidateTenantAndCompany(Guid tenantId, Guid companyId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
    }
}
