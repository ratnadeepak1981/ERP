using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class UnitOfMeasureService : IUnitOfMeasureService
{
    private readonly IUnitOfMeasureRepository _repository;

    public UnitOfMeasureService(IUnitOfMeasureRepository repository)
    {
        _repository = repository;
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
}
