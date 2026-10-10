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
