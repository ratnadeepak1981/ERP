using System;

namespace Domain.Features.MasterData.Product;

public record EffectivePlanningSettings(
    decimal ReorderLevel,
    decimal ReorderQuantity,
    decimal? MinStockQuantity,
    decimal? MaxStockQuantity,
    decimal? SafetyStockQuantity,
    int? LeadTimeDays,
    bool IsWarehouseOverrideApplied,
    Guid? WarehouseId = null)
{
    public void Validate()
    {
        if (ReorderLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(ReorderLevel), "Effective reorder level cannot be negative.");

        if (ReorderQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(ReorderQuantity), "Effective reorder quantity cannot be negative.");

        if (MinStockQuantity.HasValue && MinStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(MinStockQuantity), "Effective minimum stock quantity cannot be negative.");

        if (MaxStockQuantity.HasValue && MaxStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(MaxStockQuantity), "Effective maximum stock quantity cannot be negative.");

        if (SafetyStockQuantity.HasValue && SafetyStockQuantity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(SafetyStockQuantity), "Effective safety stock quantity cannot be negative.");

        if (LeadTimeDays.HasValue && LeadTimeDays.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(LeadTimeDays), "Effective lead time days cannot be negative.");

        if (MinStockQuantity.HasValue && MaxStockQuantity.HasValue && MinStockQuantity.Value > MaxStockQuantity.Value)
            throw new InvalidOperationException("Effective minimum stock quantity cannot be greater than effective maximum stock quantity.");

        if (SafetyStockQuantity.HasValue && MaxStockQuantity.HasValue && SafetyStockQuantity.Value > MaxStockQuantity.Value)
            throw new InvalidOperationException("Effective safety stock quantity cannot be greater than effective maximum stock quantity.");

        if (SafetyStockQuantity.HasValue && MinStockQuantity.HasValue && SafetyStockQuantity.Value > MinStockQuantity.Value)
            throw new InvalidOperationException("Effective safety stock quantity cannot be greater than effective minimum stock quantity.");
    }
}
