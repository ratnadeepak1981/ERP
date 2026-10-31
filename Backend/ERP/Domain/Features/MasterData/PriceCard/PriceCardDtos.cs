using System;
using System.Collections.Generic;

namespace Domain.Features.MasterData.PriceCard;

public class PriceCardDto
{
    public Guid PriceCardId { get; set; }
    public Guid TenantId { get; set; }
    public string PriceCardCode { get; set; } = string.Empty;
    public string PriceCardName { get; set; } = string.Empty;
    public PriceCardType PriceCardType { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int LineCount { get; set; }
    public List<PriceCardLineDto> Lines { get; set; } = new();
}

public class PriceCardLineDto
{
    public Guid PriceCardLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PriceCardId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureCode { get; set; }
    public string? UnitOfMeasureName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinimumQuantity { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePriceCardRequest
{
    public string PriceCardCode { get; set; } = string.Empty;
    public string PriceCardName { get; set; } = string.Empty;
    public PriceCardType PriceCardType { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public List<CreatePriceCardLineRequest>? Lines { get; set; }
}

public class UpdatePriceCardRequest
{
    public string PriceCardName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Description { get; set; }
    public List<CreatePriceCardLineRequest>? Lines { get; set; }
}

public class CreatePriceCardLineRequest
{
    public Guid ProductId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinimumQuantity { get; set; } = 1m;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class UpdatePriceCardLineRequest
{
    public Guid? UnitOfMeasureId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinimumQuantity { get; set; } = 1m;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class PriceResolutionResult
{
    public bool Found { get; set; }
    public Guid? PriceCardId { get; set; }
    public string? PriceCardCode { get; set; }
    public string? PriceCardName { get; set; }
    public PriceCardType? PriceCardType { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ResolvedUomId { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Source { get; set; } // "PriceCard", "SupplierProductPrice", "ProductMaster"
    public string? Message { get; set; }
}
