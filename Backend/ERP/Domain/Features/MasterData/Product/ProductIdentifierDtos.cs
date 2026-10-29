using System;
using Domain.Features.MasterData.Common;

namespace Domain.Features.MasterData.Product;

public class ProductIdentifierDto
{
    public Guid ProductIdentifierId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureCode { get; set; }
    public string IdentifierValue { get; set; } = string.Empty;
    public IdentifierType IdentifierType { get; set; }
    public IdentifierSymbology Symbology { get; set; }
    public IdentifierSource Source { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProductIdentifierRequest
{
    public string IdentifierValue { get; set; } = string.Empty;
    public IdentifierType IdentifierType { get; set; } = IdentifierType.LinearBarcode;
    public IdentifierSymbology Symbology { get; set; } = IdentifierSymbology.Code128;
    public IdentifierSource Source { get; set; } = IdentifierSource.Manufacturer;
    public Guid? UnitOfMeasureId { get; set; }
    public bool IsPrimary { get; set; }
}

public class GenerateInternalProductIdentifierRequest
{
    public IdentifierType IdentifierType { get; set; } = IdentifierType.LinearBarcode;
    public IdentifierSymbology Symbology { get; set; } = IdentifierSymbology.Code128;
    public Guid? UnitOfMeasureId { get; set; }
    public bool IsPrimary { get; set; }
}
