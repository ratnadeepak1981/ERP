using System;

namespace ERP.Domain.Features.Inventory.Planning;

public enum ReplenishmentUrgency
{
    Normal = 0,
    LowStock = 1,
    CriticalStockoutRisk = 2
}

public class ReplenishmentSuggestionDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid? WarehouseId { get; set; }
    public string? WarehouseCode { get; set; }
    public string? WarehouseName { get; set; }

    // Inventory Balances Breakdown
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityQuarantine { get; set; }
    public decimal ExpiredQuantity { get; set; }
    public decimal EligibleOnHandQuantity { get; set; }
    public decimal QuantityOnOrder { get; set; }
    public decimal NetAvailablePlanningStock { get; set; }

    // Effective Planning Settings (after precedence resolution)
    public decimal EffectiveReorderLevel { get; set; }
    public decimal EffectiveReorderQuantity { get; set; }
    public decimal? EffectiveMinStockQuantity { get; set; }
    public decimal? EffectiveMaxStockQuantity { get; set; }
    public decimal? EffectiveSafetyStockQuantity { get; set; }
    public int? EffectiveLeadTimeDays { get; set; }
    public bool IsWarehouseOverrideApplied { get; set; }

    // Trigger flags & Urgency
    public bool IsReorderTriggered { get; set; }
    public bool IsBelowMinimumStock { get; set; }
    public bool IsBelowSafetyStock { get; set; }
    public ReplenishmentUrgency Urgency { get; set; }
    public string UrgencyDescription { get; set; } = string.Empty;

    // Replenishment Sizing & Warnings
    public decimal SuggestedReplenishmentQuantity { get; set; }
    public string? ConfigurationWarning { get; set; }
}
