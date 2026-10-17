using System;
using System.Collections.Generic;
using Domain.Features.MasterData.Product;

namespace ERP.Domain.Features.Inventory.Valuation;

public class ValuationContext
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid WarehouseLocationId { get; set; }
    public Product Product { get; set; } = null!;
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public class ValuationInflowResult
{
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal VarianceAmount { get; set; }
}

public class ValuationOutflowResult
{
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public List<(Guid CostLayerId, decimal QuantityConsumed, decimal CostConsumed)> ConsumedLayers { get; set; } = new();
}

public class ValuationReversalResult
{
    public decimal ReversalUnitCost { get; set; }
    public decimal ReversalTotalCost { get; set; }
    public decimal VarianceAmount { get; set; }
}
