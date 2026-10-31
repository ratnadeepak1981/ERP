using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.UnitOfMeasure;
using UnitOfMeasureEntity = Domain.Features.MasterData.UnitOfMeasure.UnitOfMeasure;

namespace Domain.Features.MasterData.Product;

public class ProductPackagingService : IProductPackagingService
{
    private readonly IProductPackagingRepository _packagingRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfMeasureRepository _uomRepository;
    private readonly ICompanyRepository? _companyRepository;
    private readonly ISpecialUomConversionSettingsProvider? _settingsProvider;

    public ProductPackagingService(
        IProductPackagingRepository packagingRepository,
        IProductRepository productRepository,
        IUnitOfMeasureRepository uomRepository,
        ICompanyRepository? companyRepository = null,
        ISpecialUomConversionSettingsProvider? settingsProvider = null)
    {
        _packagingRepository = packagingRepository;
        _productRepository = productRepository;
        _uomRepository = uomRepository;
        _companyRepository = companyRepository;
        _settingsProvider = settingsProvider;
    }

    public async Task<ProductPackagingDto> CreatePackagingAsync(Guid tenantId, CreateProductPackagingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.CompanyId.HasValue && _settingsProvider != null)
        {
            bool isSpecialEnabled = await _settingsProvider.IsSpecialUomConversionEnabledAsync(tenantId, request.CompanyId.Value);
            if (!isSpecialEnabled)
            {
                throw new InvalidOperationException("Special UOM conversion is disabled for the specified company.");
            }
        }

        var product = await _productRepository.GetByIdAsync(tenantId, request.ProductId)
            ?? throw new KeyNotFoundException("Product not found or does not belong to the current tenant.");

        var packagingUom = await _uomRepository.GetUnitByIdAsync(tenantId, request.PackagingUomId)
            ?? throw new KeyNotFoundException("Packaging unit of measure not found or does not belong to the current tenant.");

        if (!packagingUom.IsActive)
            throw new InvalidOperationException($"Packaging unit of measure '{packagingUom.Code}' is inactive.");

        if (product.UnitOfMeasureId.HasValue && product.UnitOfMeasureId.Value == request.PackagingUomId)
            throw new InvalidOperationException($"Packaging UOM cannot be the same as the product's base stock UOM ('{packagingUom.Code}').");

        if (request.CompanyId.HasValue && _companyRepository != null)
        {
            var company = await _companyRepository.GetByIdAsync(tenantId, request.CompanyId.Value);
            if (company == null || company.TenantId != tenantId)
                throw new InvalidOperationException("Specified company does not belong to the current tenant.");
        }

        string code = request.PackagingCode.Trim().ToUpperInvariant();
        if (await _packagingRepository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Packaging code '{code}' already exists for this tenant.");

        var packaging = ProductPackaging.Create(
            tenantId,
            request.ProductId,
            code,
            request.PackagingName,
            request.PackagingUomId,
            request.BaseQuantity,
            request.PurchaseAllowed,
            request.SalesAllowed,
            request.CompanyId);

        await _packagingRepository.AddAsync(packaging);
        await _packagingRepository.SaveChangesAsync();

        return await MapToDtoAsync(tenantId, packaging, product, packagingUom);
    }

    public async Task<ProductPackagingDto> UpdatePackagingAsync(Guid tenantId, Guid productPackagingId, UpdateProductPackagingRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.CompanyId.HasValue && _settingsProvider != null)
        {
            bool isSpecialEnabled = await _settingsProvider.IsSpecialUomConversionEnabledAsync(tenantId, request.CompanyId.Value);
            if (!isSpecialEnabled)
            {
                throw new InvalidOperationException("Special UOM conversion is disabled for the specified company.");
            }
        }

        var packaging = await _packagingRepository.GetByIdAsync(tenantId, productPackagingId)
            ?? throw new KeyNotFoundException("Product packaging definition not found.");

        if (request.CompanyId.HasValue && _companyRepository != null)
        {
            var company = await _companyRepository.GetByIdAsync(tenantId, request.CompanyId.Value);
            if (company == null || company.TenantId != tenantId)
                throw new InvalidOperationException("Specified company does not belong to the current tenant.");
        }

        packaging.Update(
            request.PackagingName,
            request.BaseQuantity,
            request.PurchaseAllowed,
            request.SalesAllowed,
            request.CompanyId);

        await _packagingRepository.SaveChangesAsync();

        var product = await _productRepository.GetByIdAsync(tenantId, packaging.ProductId);
        var uom = await _uomRepository.GetUnitByIdAsync(tenantId, packaging.PackagingUomId);
        return await MapToDtoAsync(tenantId, packaging, product, uom);
    }

    public async Task DeactivatePackagingAsync(Guid tenantId, Guid productPackagingId)
    {
        var packaging = await _packagingRepository.GetByIdAsync(tenantId, productPackagingId)
            ?? throw new KeyNotFoundException("Product packaging definition not found.");

        packaging.Deactivate();
        await _packagingRepository.SaveChangesAsync();
    }

    public async Task<ProductPackagingDto> ReactivatePackagingAsync(Guid tenantId, Guid productPackagingId)
    {
        var packaging = await _packagingRepository.GetByIdAsync(tenantId, productPackagingId)
            ?? throw new KeyNotFoundException("Product packaging definition not found.");

        packaging.Activate();
        await _packagingRepository.SaveChangesAsync();

        var product = await _productRepository.GetByIdAsync(tenantId, packaging.ProductId);
        var uom = await _uomRepository.GetUnitByIdAsync(tenantId, packaging.PackagingUomId);
        return await MapToDtoAsync(tenantId, packaging, product, uom);
    }

    public async Task<ProductPackagingDto?> GetPackagingByIdAsync(Guid tenantId, Guid productPackagingId)
    {
        var packaging = await _packagingRepository.GetByIdAsync(tenantId, productPackagingId);
        if (packaging == null) return null;

        var product = await _productRepository.GetByIdAsync(tenantId, packaging.ProductId);
        var uom = await _uomRepository.GetUnitByIdAsync(tenantId, packaging.PackagingUomId);
        return await MapToDtoAsync(tenantId, packaging, product, uom);
    }

    public async Task<List<ProductPackagingDto>> GetPackagingsByProductAsync(Guid tenantId, Guid productId, bool includeInactive = false)
    {
        var list = await _packagingRepository.GetByProductIdAsync(tenantId, productId, includeInactive);
        var product = await _productRepository.GetByIdAsync(tenantId, productId);
        var uoms = (await _uomRepository.GetUnitsAsync(tenantId)).ToDictionary(u => u.UnitOfMeasureId);

        var result = new List<ProductPackagingDto>();
        foreach (var p in list)
        {
            uoms.TryGetValue(p.PackagingUomId, out var uom);
            result.Add(await MapToDtoAsync(tenantId, p, product, uom));
        }
        return result;
    }

    public async Task<List<ProductPackagingDto>> GetPackagingsAsync(Guid tenantId, Guid? productId = null, Guid? companyId = null, bool includeInactive = false)
    {
        var list = await _packagingRepository.GetByTenantAsync(tenantId, productId, companyId, includeInactive);
        var products = (await _productRepository.GetByTenantAsync(tenantId, null)).ToDictionary(p => p.ProductId);
        var uoms = (await _uomRepository.GetUnitsAsync(tenantId)).ToDictionary(u => u.UnitOfMeasureId);

        var result = new List<ProductPackagingDto>();
        foreach (var p in list)
        {
            products.TryGetValue(p.ProductId, out var prod);
            uoms.TryGetValue(p.PackagingUomId, out var uom);
            result.Add(await MapToDtoAsync(tenantId, p, prod, uom));
        }
        return result;
    }

    private async Task<ProductPackagingDto> MapToDtoAsync(
        Guid tenantId,
        ProductPackaging packaging,
        Product? product,
        UnitOfMeasureEntity? packagingUom)
    {
        string prodCode = product?.ProductCode ?? string.Empty;
        string prodName = product?.ProductName ?? string.Empty;
        string pkgUomCode = packagingUom?.Code ?? "UOM";

        string baseUomCode = product?.UnitOfMeasure ?? "PCS";
        Guid baseUomId = product?.UnitOfMeasureId ?? Guid.Empty;

        if (baseUomId == Guid.Empty && product != null)
        {
            var allUoms = await _uomRepository.GetUnitsAsync(tenantId);
            var match = allUoms.FirstOrDefault(u => u.Code.Equals(product.UnitOfMeasure, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                baseUomId = match.UnitOfMeasureId;
                baseUomCode = match.Code;
            }
        }

        string displayExpression = $"1 {pkgUomCode} = {packaging.BaseQuantity:G29} {baseUomCode}";

        return new ProductPackagingDto
        {
            ProductPackagingId = packaging.ProductPackagingId,
            TenantId = packaging.TenantId,
            CompanyId = packaging.CompanyId,
            ProductId = packaging.ProductId,
            ProductCode = prodCode,
            ProductName = prodName,
            PackagingCode = packaging.PackagingCode,
            PackagingName = packaging.PackagingName,
            PackagingUomId = packaging.PackagingUomId,
            PackagingUomCode = pkgUomCode,
            ProductBaseUomId = baseUomId,
            ProductBaseUomCode = baseUomCode,
            BaseQuantity = packaging.BaseQuantity,
            DisplayExpression = displayExpression,
            PurchaseAllowed = packaging.PurchaseAllowed,
            SalesAllowed = packaging.SalesAllowed,
            IsActive = packaging.IsActive
        };
    }
}
