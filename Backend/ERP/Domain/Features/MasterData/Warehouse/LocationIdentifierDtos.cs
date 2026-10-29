using System;
using Domain.Features.MasterData.Common;

namespace ERP.Domain.Features.MasterData.Warehouse;

public class LocationIdentifierDto
{
    public Guid LocationIdentifierId { get; set; }
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public string IdentifierValue { get; set; } = string.Empty;
    public IdentifierType IdentifierType { get; set; }
    public IdentifierSymbology Symbology { get; set; }
    public IdentifierSource Source { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateLocationIdentifierRequest
{
    public string IdentifierValue { get; set; } = string.Empty;
    public IdentifierType IdentifierType { get; set; } = IdentifierType.LinearBarcode;
    public IdentifierSymbology Symbology { get; set; } = IdentifierSymbology.Code128;
    public IdentifierSource Source { get; set; } = IdentifierSource.Internal;
    public bool IsPrimary { get; set; } = true;
}

public class GenerateInternalLocationIdentifierRequest
{
    public IdentifierType IdentifierType { get; set; } = IdentifierType.LinearBarcode;
    public IdentifierSymbology Symbology { get; set; } = IdentifierSymbology.Code128;
    public bool IsPrimary { get; set; } = true;
}
