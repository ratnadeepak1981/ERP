using System;

namespace Domain.Features.MasterData.UnitOfMeasure;

public class CreateUnitOfMeasureRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateUnitOfMeasureRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateUnitOfMeasureConversionRequest
{
    public Guid FromUnitId { get; set; }
    public Guid ToUnitId { get; set; }
    public decimal ConversionFactor { get; set; }
}

public class UpdateUnitOfMeasureConversionRequest
{
    public decimal ConversionFactor { get; set; }
}

public enum ConversionContext
{
    General = 0,
    Purchase = 1,
    Sales = 2
}

public class ConvertProductQuantityRequest
{
    public Guid ProductId { get; set; }
    public Guid FromUnitId { get; set; }
    public Guid ToUnitId { get; set; }
    public decimal Quantity { get; set; }
    public ConversionContext Context { get; set; } = ConversionContext.General;
    public Guid? ProductPackagingId { get; set; }
    public Guid? CompanyId { get; set; }
}

public class ConvertUnitOfMeasureResult
{
    public Guid FromUnitId { get; set; }
    public Guid ToUnitId { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal ConvertedQuantity { get; set; }
    public decimal EffectiveFactor { get; set; }
    public bool IsInverse { get; set; }
    public bool IsSpecialConversion { get; set; }
    public string? PackagingCode { get; set; }
    public Guid? ProductPackagingId { get; set; }
}
