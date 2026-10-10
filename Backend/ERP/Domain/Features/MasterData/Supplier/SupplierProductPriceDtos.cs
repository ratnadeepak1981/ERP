using System;
using System.Collections.Generic;

namespace Domain.Features.MasterData.Supplier;

public class CreateSupplierProductPriceRequest
{
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }
    public string? SupplierItemCode { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? UnitOfMeasureId { get; set; }
    public decimal MinimumOrderQuantity { get; set; } = 1;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsPreferred { get; set; }
}

public class UpdateSupplierProductPriceRequest
{
    public string? SupplierItemCode { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? UnitOfMeasureId { get; set; }
    public decimal MinimumOrderQuantity { get; set; } = 1;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsPreferred { get; set; }
}
