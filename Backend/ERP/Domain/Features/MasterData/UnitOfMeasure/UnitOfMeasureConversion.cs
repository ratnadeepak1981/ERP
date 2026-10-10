using System;
using ERP.Domain.Common;
using ERP.Domain.Common.Scope;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class UnitOfMeasureConversion : Auditable, ITenantScopedEntity
{
    public Guid ConversionId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid FromUnitId { get; private set; }

    public Guid ToUnitId { get; private set; }

    public decimal ConversionFactor { get; private set; }

    public bool IsActive { get; private set; }

    public static UnitOfMeasureConversion Create(
        Guid tenantId,
        Guid fromUnitId,
        Guid toUnitId,
        decimal conversionFactor,
        Guid? conversionId = null)
    {
        if (fromUnitId == toUnitId)
        {
            throw new ArgumentException("Cannot convert a unit of measure to itself.");
        }

        if (conversionFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactor), "Conversion factor must be greater than zero.");
        }

        return new UnitOfMeasureConversion
        {
            ConversionId = conversionId ?? Guid.NewGuid(),
            TenantId = tenantId,
            FromUnitId = fromUnitId,
            ToUnitId = toUnitId,
            ConversionFactor = conversionFactor,
            IsActive = true
        };
    }

    public void UpdateFactor(decimal conversionFactor)
    {
        if (conversionFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactor), "Conversion factor must be greater than zero.");
        }

        ConversionFactor = conversionFactor;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private UnitOfMeasureConversion()
    {
    }
}
