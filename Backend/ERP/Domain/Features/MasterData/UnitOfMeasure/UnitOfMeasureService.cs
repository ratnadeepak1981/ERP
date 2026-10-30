using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class UnitOfMeasureService : IUnitOfMeasureService
{
    private readonly IUnitOfMeasureRepository _repository;
    private readonly IProductRepository? _productRepository;
    private readonly IProductPackagingRepository? _packagingRepository;
    private readonly ISpecialUomConversionSettingsProvider? _settingsProvider;

    public UnitOfMeasureService(
        IUnitOfMeasureRepository repository,
        IProductRepository? productRepository = null,
        IProductPackagingRepository? packagingRepository = null,
        ISpecialUomConversionSettingsProvider? settingsProvider = null)
    {
        _repository = repository;
        _productRepository = productRepository;
        _packagingRepository = packagingRepository;
        _settingsProvider = settingsProvider;
    }

    public async Task<List<UnitOfMeasure>> GetUnitsAsync(Guid tenantId)
    {
        return await _repository.GetUnitsAsync(tenantId);
    }

    public async Task<UnitOfMeasure?> GetUnitAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        return await _repository.GetUnitByIdAsync(tenantId, unitOfMeasureId);
    }

    public async Task<UnitOfMeasure> CreateUnitAsync(Guid tenantId, CreateUnitOfMeasureRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Unit code is required.", nameof(request.Code));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Unit name is required.", nameof(request.Name));

        string code = request.Code.Trim().ToUpperInvariant();
        if (await _repository.ExistsByCodeAsync(tenantId, code))
            throw new InvalidOperationException($"Unit of measure code '{code}' already exists for this tenant.");

        var unit = UnitOfMeasure.Create(tenantId, code, request.Name.Trim(), request.Description?.Trim());
        await _repository.AddUnitAsync(unit);
        await _repository.SaveChangesAsync();
        return unit;
    }

    public async Task<UnitOfMeasure> UpdateUnitAsync(Guid tenantId, Guid unitOfMeasureId, UpdateUnitOfMeasureRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Unit name is required.", nameof(request.Name));

        var unit = await _repository.GetUnitByIdAsync(tenantId, unitOfMeasureId)
            ?? throw new KeyNotFoundException("Unit of measure not found.");

        unit.Update(request.Name.Trim(), request.Description?.Trim());
        await _repository.SaveChangesAsync();
        return unit;
    }

    public async Task DeactivateUnitAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        var unit = await _repository.GetUnitByIdAsync(tenantId, unitOfMeasureId)
            ?? throw new KeyNotFoundException("Unit of measure not found.");

        unit.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<UnitOfMeasure> ReactivateUnitAsync(Guid tenantId, Guid unitOfMeasureId)
    {
        var unit = await _repository.GetUnitByIdAsync(tenantId, unitOfMeasureId)
            ?? throw new KeyNotFoundException("Unit of measure not found.");

        if (unit.IsActive)
            return unit;

        unit.Activate();
        await _repository.SaveChangesAsync();
        return unit;
    }

    public async Task<List<UnitOfMeasureConversion>> GetConversionsAsync(Guid tenantId)
    {
        return await _repository.GetConversionsAsync(tenantId);
    }

    public async Task<UnitOfMeasureConversion> CreateConversionAsync(Guid tenantId, CreateUnitOfMeasureConversionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.FromUnitId == request.ToUnitId)
            throw new ArgumentException("FromUnit and ToUnit must be different units of measure.");

        if (request.ConversionFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.ConversionFactor), "Conversion factor must be greater than zero.");

        // Tenant ownership verification for both units
        var fromUnit = await _repository.GetUnitByIdAsync(tenantId, request.FromUnitId);
        if (fromUnit == null)
            throw new InvalidOperationException("FromUnit not found or does not belong to the current tenant.");

        var toUnit = await _repository.GetUnitByIdAsync(tenantId, request.ToUnitId);
        if (toUnit == null)
            throw new InvalidOperationException("ToUnit not found or does not belong to the current tenant.");

        var existing = await _repository.GetConversionAsync(tenantId, request.FromUnitId, request.ToUnitId);
        if (existing != null)
            throw new InvalidOperationException("A conversion between these units already exists for this tenant.");

        var conversion = UnitOfMeasureConversion.Create(tenantId, request.FromUnitId, request.ToUnitId, request.ConversionFactor);
        await _repository.AddConversionAsync(conversion);
        await _repository.SaveChangesAsync();
        return conversion;
    }

    public async Task<UnitOfMeasureConversion> UpdateConversionAsync(Guid tenantId, Guid conversionId, UpdateUnitOfMeasureConversionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.ConversionFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.ConversionFactor), "Conversion factor must be greater than zero.");

        var conversion = await _repository.GetConversionByIdAsync(tenantId, conversionId)
            ?? throw new KeyNotFoundException("Unit of measure conversion not found.");

        conversion.UpdateFactor(request.ConversionFactor);
        await _repository.SaveChangesAsync();
        return conversion;
    }

    public async Task DeactivateConversionAsync(Guid tenantId, Guid conversionId)
    {
        var conversion = await _repository.GetConversionByIdAsync(tenantId, conversionId)
            ?? throw new KeyNotFoundException("Unit of measure conversion not found.");

        conversion.Deactivate();
        await _repository.SaveChangesAsync();
    }

    public async Task<ConvertUnitOfMeasureResult> ConvertQuantityAsync(Guid tenantId, Guid fromUnitId, Guid toUnitId, decimal quantity)
    {
        var fromUnit = await _repository.GetUnitByIdAsync(tenantId, fromUnitId);
        if (fromUnit == null || !fromUnit.IsActive)
            throw new InvalidOperationException("Source unit of measure not found or is inactive.");

        var toUnit = await _repository.GetUnitByIdAsync(tenantId, toUnitId);
        if (toUnit == null || !toUnit.IsActive)
            throw new InvalidOperationException("Target unit of measure not found or is inactive.");

        if (fromUnitId == toUnitId)
        {
            return new ConvertUnitOfMeasureResult
            {
                FromUnitId = fromUnitId,
                ToUnitId = toUnitId,
                OriginalQuantity = quantity,
                ConvertedQuantity = quantity,
                EffectiveFactor = 1m,
                IsInverse = false
            };
        }

        // 1. Check direct conversion
        var direct = await _repository.GetConversionAsync(tenantId, fromUnitId, toUnitId);
        if (direct != null && direct.IsActive)
        {
            if (direct.ConversionFactor <= 0)
                throw new InvalidOperationException("Conversion factor must be greater than zero.");

            return new ConvertUnitOfMeasureResult
            {
                FromUnitId = fromUnitId,
                ToUnitId = toUnitId,
                OriginalQuantity = quantity,
                ConvertedQuantity = Math.Round(quantity * direct.ConversionFactor, 6, MidpointRounding.AwayFromZero),
                EffectiveFactor = direct.ConversionFactor,
                IsInverse = false
            };
        }

        // 2. Check inverse conversion
        var inverse = await _repository.GetConversionAsync(tenantId, toUnitId, fromUnitId);
        if (inverse != null && inverse.IsActive)
        {
            if (inverse.ConversionFactor <= 0)
                throw new InvalidOperationException("Conversion factor must be greater than zero.");

            decimal effectiveFactor = Math.Round(1m / inverse.ConversionFactor, 6, MidpointRounding.AwayFromZero);
            decimal convertedQty = Math.Round(quantity / inverse.ConversionFactor, 6, MidpointRounding.AwayFromZero);

            return new ConvertUnitOfMeasureResult
            {
                FromUnitId = fromUnitId,
                ToUnitId = toUnitId,
                OriginalQuantity = quantity,
                ConvertedQuantity = convertedQty,
                EffectiveFactor = effectiveFactor,
                IsInverse = true
            };
        }

        throw new InvalidOperationException($"No active unit conversion found between '{fromUnit.Code}' and '{toUnit.Code}' for this tenant.");
    }

    public async Task<ConvertUnitOfMeasureResult> ConvertProductQuantityAsync(
        Guid tenantId,
        Guid productId,
        Guid fromUnitId,
        Guid toUnitId,
        decimal quantity,
        ConversionContext context = ConversionContext.General,
        Guid? productPackagingId = null,
        Guid? companyId = null)
    {
        if (fromUnitId == toUnitId)
        {
            return new ConvertUnitOfMeasureResult
            {
                FromUnitId = fromUnitId,
                ToUnitId = toUnitId,
                OriginalQuantity = quantity,
                ConvertedQuantity = quantity,
                EffectiveFactor = 1m,
                IsInverse = false,
                IsSpecialConversion = false
            };
        }

        if (_productRepository == null)
            throw new InvalidOperationException("Product repository is required for product-specific UOM conversions.");

        var product = await _productRepository.GetByIdAsync(tenantId, productId)
            ?? throw new KeyNotFoundException("Product not found or does not belong to the current tenant.");

        // Check if Special UOM conversion is enabled for this tenant / company
        bool isSpecialEnabled = _settingsProvider != null && await _settingsProvider.IsSpecialUomConversionEnabledAsync(tenantId, companyId);

        if (productPackagingId.HasValue && !isSpecialEnabled)
        {
            if (companyId.HasValue)
            {
                throw new InvalidOperationException("Special/Product-Specific UOM Conversion is disabled for this company.");
            }
            throw new InvalidOperationException("Special/Product-Specific UOM Conversion is disabled for this tenant.");
        }

        if (isSpecialEnabled && _packagingRepository != null)
        {
            ProductPackaging? packaging = null;

            if (productPackagingId.HasValue)
            {
                var candidate = await _packagingRepository.GetByIdAsync(tenantId, productPackagingId.Value);
                if (candidate != null && candidate.ProductId == productId && candidate.IsActive)
                {
                    packaging = candidate;
                }
                else
                {
                    throw new InvalidOperationException("Specified product packaging is not found, inactive, or does not belong to this product.");
                }
            }
            else
            {
                var directPackagings = await _packagingRepository.GetByProductAndUomAsync(tenantId, productId, fromUnitId);
                var matchingDirect = directPackagings.Where(p => p.IsActive).ToList();

                if (matchingDirect.Count == 1)
                {
                    packaging = matchingDirect[0];
                }
                else if (matchingDirect.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple active packaging definitions exist for product '{product.ProductCode}' and unit. Please specify the packaging definition explicitly.");
                }
                else
                {
                    var inversePackagings = await _packagingRepository.GetByProductAndUomAsync(tenantId, productId, toUnitId);
                    var matchingInverse = inversePackagings.Where(p => p.IsActive).ToList();
                    if (matchingInverse.Count == 1)
                    {
                        packaging = matchingInverse[0];
                    }
                    else if (matchingInverse.Count > 1)
                    {
                        throw new InvalidOperationException(
                            $"Multiple active packaging definitions exist for product '{product.ProductCode}' and unit. Please specify the packaging definition explicitly.");
                    }
                }
            }

            if (packaging != null)
            {
                if (companyId.HasValue && packaging.CompanyId.HasValue && packaging.CompanyId.Value != companyId.Value)
                {
                    throw new InvalidOperationException(
                        $"Product packaging '{packaging.PackagingCode}' is not eligible for company '{companyId.Value}'.");
                }

                if (context == ConversionContext.Purchase && !packaging.PurchaseAllowed)
                {
                    throw new InvalidOperationException(
                        $"Product packaging '{packaging.PackagingCode}' is not allowed for purchasing.");
                }

                if (context == ConversionContext.Sales && !packaging.SalesAllowed)
                {
                    throw new InvalidOperationException(
                        $"Product packaging '{packaging.PackagingCode}' is not allowed for sales.");
                }

                if (packaging.BaseQuantity <= 0)
                    throw new InvalidOperationException("Packaging base quantity must be greater than zero.");

                if (fromUnitId == packaging.PackagingUomId)
                {
                    decimal convertedQty = Math.Round(quantity * packaging.BaseQuantity, 6, MidpointRounding.AwayFromZero);
                    return new ConvertUnitOfMeasureResult
                    {
                        FromUnitId = fromUnitId,
                        ToUnitId = toUnitId,
                        OriginalQuantity = quantity,
                        ConvertedQuantity = convertedQty,
                        EffectiveFactor = packaging.BaseQuantity,
                        IsInverse = false,
                        IsSpecialConversion = true,
                        PackagingCode = packaging.PackagingCode,
                        ProductPackagingId = packaging.ProductPackagingId
                    };
                }
                else if (toUnitId == packaging.PackagingUomId)
                {
                    decimal effectiveFactor = Math.Round(1m / packaging.BaseQuantity, 6, MidpointRounding.AwayFromZero);
                    decimal convertedQty = Math.Round(quantity / packaging.BaseQuantity, 6, MidpointRounding.AwayFromZero);
                    return new ConvertUnitOfMeasureResult
                    {
                        FromUnitId = fromUnitId,
                        ToUnitId = toUnitId,
                        OriginalQuantity = quantity,
                        ConvertedQuantity = convertedQty,
                        EffectiveFactor = effectiveFactor,
                        IsInverse = true,
                        IsSpecialConversion = true,
                        PackagingCode = packaging.PackagingCode,
                        ProductPackagingId = packaging.ProductPackagingId
                    };
                }
            }
        }

        return await ConvertQuantityAsync(tenantId, fromUnitId, toUnitId, quantity);
    }
}
