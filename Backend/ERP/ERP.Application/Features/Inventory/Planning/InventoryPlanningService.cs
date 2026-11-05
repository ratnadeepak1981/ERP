using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Planning;

namespace ERP.Application.Features.Inventory.Planning;

public class InventoryPlanningService : IInventoryPlanningService
{
    private readonly IInventoryPersistenceCoordinator _persistenceCoordinator;

    public InventoryPlanningService(IInventoryPersistenceCoordinator persistenceCoordinator)
    {
        _persistenceCoordinator = persistenceCoordinator ?? throw new ArgumentNullException(nameof(persistenceCoordinator));
    }

    public async Task<List<ReplenishmentSuggestionDto>> GetReplenishmentSuggestionsAsync(
        Guid tenantId,
        Guid? warehouseId = null,
        Guid? categoryId = null,
        DateTime? asOfDate = null)
    {
        var effectiveDate = (asOfDate ?? DateTime.UtcNow).Date;

        var data = await _persistenceCoordinator.GetPlanningDataAsync(tenantId, warehouseId, categoryId);
        if (data.TargetWarehouses.Count == 0 || data.Products.Count == 0)
        {
            return new List<ReplenishmentSuggestionDto>();
        }

        var settingsLookup = data.WarehouseSettings
            .ToDictionary(s => (s.ProductId, s.WarehouseId));

        var warehousesByBranch = data.TargetWarehouses
            .Where(w => w.BranchId.HasValue)
            .GroupBy(w => w.BranchId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var balancesByWhAndProduct = data.Balances
            .GroupBy(b => (b.WarehouseId, b.ProductId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var poQtyByBranchAndProduct = data.ApprovedPoLines
            .GroupBy(x => (x.BranchId, x.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OutstandingQty));

        var suggestions = new List<ReplenishmentSuggestionDto>();

        foreach (var warehouse in data.TargetWarehouses)
        {
            foreach (var product in data.Products)
            {
                balancesByWhAndProduct.TryGetValue((warehouse.WarehouseId, product.ProductId), out var whBalances);
                whBalances ??= new List<InventoryBalance>();

                if (!warehouseId.HasValue && data.TargetWarehouses.Count > 1)
                {
                    bool hasBalance = whBalances.Count > 0;
                    bool hasSetting = settingsLookup.ContainsKey((product.ProductId, warehouse.WarehouseId));
                    bool hasPo = warehouse.BranchId.HasValue && poQtyByBranchAndProduct.ContainsKey((warehouse.BranchId.Value, product.ProductId));

                    if (!hasBalance && !hasSetting && !hasPo)
                    {
                        continue;
                    }
                }

                decimal totalOnHand = 0m;
                decimal totalAllocated = 0m;
                decimal totalQuarantine = 0m;
                decimal expiredQuantity = 0m;

                bool isBatchTracked = product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial;

                foreach (var b in whBalances)
                {
                    totalOnHand += b.QuantityOnHand;
                    totalAllocated += b.QuantityAllocated;
                    totalQuarantine += b.QuantityQuarantine;

                    if (isBatchTracked && !string.IsNullOrWhiteSpace(b.BatchNumber) && b.QuantityOnHand > 0)
                    {
                        if (data.BatchExpiryDict.TryGetValue((product.ProductId, b.BatchNumber), out var expDate) && expDate.HasValue)
                        {
                            if (expDate.Value.Date < effectiveDate)
                            {
                                expiredQuantity += b.QuantityOnHand;
                            }
                        }
                    }
                }

                decimal eligibleOnHand = Math.Max(0m, totalOnHand - expiredQuantity);

                decimal balanceOnOrder = whBalances.Sum(b => b.QuantityOnOrder);
                decimal poLineOutstanding = 0m;
                if (warehouse.BranchId.HasValue &&
                    warehousesByBranch.TryGetValue(warehouse.BranchId.Value, out var branchWarehouses) &&
                    branchWarehouses.Count == 1)
                {
                    if (poQtyByBranchAndProduct.TryGetValue((warehouse.BranchId.Value, product.ProductId), out var poQty))
                    {
                        poLineOutstanding = poQty;
                    }
                }

                decimal quantityOnOrder = Math.Max(balanceOnOrder, poLineOutstanding);
                decimal netAvailable = eligibleOnHand - totalAllocated - totalQuarantine + quantityOnOrder;

                settingsLookup.TryGetValue((product.ProductId, warehouse.WarehouseId), out var overrideSetting);
                var effectiveSettings = product.GetEffectivePlanningSettings(overrideSetting);

                bool isReorderTriggered = false;
                bool isBelowMin = false;
                bool isBelowSafety = false;

                if (effectiveSettings.SafetyStockQuantity.HasValue && effectiveSettings.SafetyStockQuantity.Value > 0)
                {
                    if (netAvailable <= effectiveSettings.SafetyStockQuantity.Value)
                    {
                        isBelowSafety = true;
                    }
                }

                if (effectiveSettings.MinStockQuantity.HasValue && effectiveSettings.MinStockQuantity.Value > 0)
                {
                    if (netAvailable <= effectiveSettings.MinStockQuantity.Value)
                    {
                        isBelowMin = true;
                    }
                }

                if (effectiveSettings.ReorderLevel > 0)
                {
                    if (netAvailable <= effectiveSettings.ReorderLevel)
                    {
                        isReorderTriggered = true;
                    }
                }
                else
                {
                    isReorderTriggered = netAvailable <= 0m;
                }

                if (isBelowMin || isBelowSafety)
                {
                    isReorderTriggered = true;
                }

                ReplenishmentUrgency urgency;
                string urgencyDescription;

                if (isBelowSafety || netAvailable <= 0m)
                {
                    urgency = ReplenishmentUrgency.CriticalStockoutRisk;
                    urgencyDescription = "Critical stockout risk: available stock has breached safety stock threshold (or is depleted/negative).";
                }
                else if (isBelowMin || isReorderTriggered)
                {
                    urgency = ReplenishmentUrgency.LowStock;
                    urgencyDescription = "Low stock: available stock has reached or fallen below reorder/minimum threshold.";
                }
                else
                {
                    urgency = ReplenishmentUrgency.Normal;
                    urgencyDescription = "Stock levels are healthy.";
                }

                decimal suggestedQuantity = 0m;
                string? configWarning = null;

                if (effectiveSettings.MaxStockQuantity.HasValue && effectiveSettings.MaxStockQuantity.Value > 0)
                {
                    decimal deficit = effectiveSettings.MaxStockQuantity.Value - netAvailable;
                    if (deficit > 0)
                    {
                        suggestedQuantity = effectiveSettings.ReorderQuantity > 0
                            ? Math.Max(deficit, effectiveSettings.ReorderQuantity)
                            : deficit;
                    }
                    else
                    {
                        suggestedQuantity = effectiveSettings.ReorderQuantity > 0
                            ? effectiveSettings.ReorderQuantity
                            : 0m;
                    }
                }
                else if (effectiveSettings.ReorderQuantity > 0)
                {
                    suggestedQuantity = effectiveSettings.ReorderQuantity;
                }
                else
                {
                    suggestedQuantity = 0m;
                    configWarning = "No valid replenishment quantity could be determined. Please configure ReorderQuantity or MaxStockQuantity.";
                }

                if (isReorderTriggered && suggestedQuantity <= 0 && configWarning == null)
                {
                    configWarning = "Replenishment is triggered, but calculated replenishment quantity is zero.";
                }

                data.Categories.TryGetValue(product.CategoryId, out var catName);

                suggestions.Add(new ReplenishmentSuggestionDto
                {
                    ProductId = product.ProductId,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    CategoryId = product.CategoryId,
                    CategoryName = catName,
                    UnitOfMeasure = product.UnitOfMeasure,
                    WarehouseId = warehouse.WarehouseId,
                    WarehouseCode = warehouse.WarehouseCode,
                    WarehouseName = warehouse.WarehouseName,
                    QuantityOnHand = totalOnHand,
                    QuantityAllocated = totalAllocated,
                    QuantityQuarantine = totalQuarantine,
                    ExpiredQuantity = expiredQuantity,
                    EligibleOnHandQuantity = eligibleOnHand,
                    QuantityOnOrder = quantityOnOrder,
                    NetAvailablePlanningStock = netAvailable,
                    EffectiveReorderLevel = effectiveSettings.ReorderLevel,
                    EffectiveReorderQuantity = effectiveSettings.ReorderQuantity,
                    EffectiveMinStockQuantity = effectiveSettings.MinStockQuantity,
                    EffectiveMaxStockQuantity = effectiveSettings.MaxStockQuantity,
                    EffectiveSafetyStockQuantity = effectiveSettings.SafetyStockQuantity,
                    EffectiveLeadTimeDays = effectiveSettings.LeadTimeDays,
                    IsWarehouseOverrideApplied = effectiveSettings.IsWarehouseOverrideApplied,
                    IsReorderTriggered = isReorderTriggered,
                    IsBelowMinimumStock = isBelowMin,
                    IsBelowSafetyStock = isBelowSafety,
                    Urgency = urgency,
                    UrgencyDescription = urgencyDescription,
                    SuggestedReplenishmentQuantity = suggestedQuantity,
                    ConfigurationWarning = configWarning
                });
            }
        }

        return suggestions;
    }
}
