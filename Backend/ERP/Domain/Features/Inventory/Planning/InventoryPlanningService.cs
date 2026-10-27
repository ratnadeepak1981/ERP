using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace ERP.Domain.Features.Inventory.Planning;

public class InventoryPlanningService : IInventoryPlanningService
{
    private readonly DomainDbContext _context;

    public InventoryPlanningService(DomainDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<ReplenishmentSuggestionDto>> GetReplenishmentSuggestionsAsync(
        Guid tenantId,
        Guid? warehouseId = null,
        Guid? categoryId = null,
        DateTime? asOfDate = null)
    {
        var effectiveDate = (asOfDate ?? DateTime.UtcNow).Date;

        // 1. Resolve target warehouses
        List<Warehouse> targetWarehouses;
        if (warehouseId.HasValue)
        {
            var wh = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.WarehouseId == warehouseId.Value && w.IsActive);

            if (wh == null)
            {
                throw new KeyNotFoundException($"Warehouse '{warehouseId.Value}' was not found, is inactive, or does not belong to this tenant.");
            }

            targetWarehouses = new List<Warehouse> { wh };
        }
        else
        {
            targetWarehouses = await _context.Warehouses
                .AsNoTracking()
                .Where(w => w.TenantId == tenantId && w.IsActive)
                .OrderBy(w => w.WarehouseCode)
                .ToListAsync();

            if (targetWarehouses.Count == 0)
            {
                return new List<ReplenishmentSuggestionDto>();
            }
        }

        var targetWarehouseIds = targetWarehouses.Select(w => w.WarehouseId).ToList();

        // 2. Resolve eligible stock-tracked products
        var productsQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.IsStockTracked);

        if (categoryId.HasValue)
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await productsQuery
            .OrderBy(p => p.ProductCode)
            .ToListAsync();

        if (products.Count == 0)
        {
            return new List<ReplenishmentSuggestionDto>();
        }

        var productIds = products.Select(p => p.ProductId).ToList();

        // Load Categories lookup for presentation
        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && categoryIds.Contains(c.CategoryId))
            .ToDictionaryAsync(c => c.CategoryId, c => c.CategoryName);

        // 3. Load all inventory balances for the products and target warehouses
        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                     && targetWarehouseIds.Contains(b.WarehouseId)
                     && productIds.Contains(b.ProductId))
            .ToListAsync();

        // 4. Load warehouse-specific planning overrides
        var warehouseSettings = await _context.ProductWarehouseSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                     && targetWarehouseIds.Contains(s.WarehouseId)
                     && productIds.Contains(s.ProductId)
                     && s.IsActive)
            .ToListAsync();

        var settingsLookup = warehouseSettings
            .ToDictionary(s => (s.ProductId, s.WarehouseId));

        // 5. Load batch expiry information for batch-tracked items
        var batchTrackedProductIds = products
            .Where(p => p.TrackingMode == TrackingMode.Batch || p.TrackingMode == TrackingMode.BatchAndSerial)
            .Select(p => p.ProductId)
            .ToList();

        Dictionary<(Guid ProductId, string BatchNumber), DateTime?> batchExpiryDict = new();
        if (batchTrackedProductIds.Count > 0)
        {
            var trackedBatches = await _context.ProductBatches
                .AsNoTracking()
                .Where(pb => pb.TenantId == tenantId
                          && batchTrackedProductIds.Contains(pb.ProductId)
                          && pb.IsActive)
                .Select(pb => new { pb.ProductId, pb.BatchNumber, pb.ExpiryDate })
                .ToListAsync();

            batchExpiryDict = trackedBatches
                .ToDictionary(b => (b.ProductId, b.BatchNumber), b => b.ExpiryDate);
        }

        // 6. Load outstanding Purchase Orders (Approved, active lines, unreceived)
        // Attribute to warehouse if PO's branch maps to exactly one active warehouse in scope
        var approvedPoLines = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId && po.Status == PurchaseOrderStatus.Approved && po.IsActive)
            .SelectMany(po => po.Items.Where(i => i.IsActive && i.Quantity > i.ReceivedQuantity), (po, item) => new
            {
                po.BranchId,
                item.ProductId,
                OutstandingQty = item.Quantity - item.ReceivedQuantity
            })
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync();

        // Map branch to target warehouses
        var warehousesByBranch = targetWarehouses
            .Where(w => w.BranchId.HasValue)
            .GroupBy(w => w.BranchId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Pre-group balances by (WarehouseId, ProductId) for O(1) lookups
        var balancesByWhAndProduct = balances
            .GroupBy(b => (b.WarehouseId, b.ProductId))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Pre-group approved PO lines by (BranchId, ProductId) for O(1) lookups
        var poQtyByBranchAndProduct = approvedPoLines
            .GroupBy(x => (x.BranchId, x.ProductId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OutstandingQty));

        var suggestions = new List<ReplenishmentSuggestionDto>();

        foreach (var warehouse in targetWarehouses)
        {
            foreach (var product in products)
            {
                // Balance subsets for this product and warehouse
                balancesByWhAndProduct.TryGetValue((warehouse.WarehouseId, product.ProductId), out var whBalances);
                whBalances ??= new List<InventoryBalance>();

                // In multi-warehouse tenant queries without a specific warehouse filter,
                // only evaluate products that are active/stocked/configured/inbound for this warehouse
                if (!warehouseId.HasValue && targetWarehouses.Count > 1)
                {
                    bool hasBalance = whBalances.Count > 0;
                    bool hasSetting = settingsLookup.ContainsKey((product.ProductId, warehouse.WarehouseId));
                    bool hasPo = warehouse.BranchId.HasValue && poQtyByBranchAndProduct.ContainsKey((warehouse.BranchId.Value, product.ProductId));

                    if (!hasBalance && !hasSetting && !hasPo)
                    {
                        continue;
                    }
                }

                decimal totalOnHand = whBalances.Sum(b => b.QuantityOnHand);
                decimal totalAllocated = whBalances.Sum(b => b.QuantityAllocated);
                decimal totalQuarantine = whBalances.Sum(b => b.QuantityQuarantine);

                // Calculate expired quantities if batch-tracked
                decimal expiredQuantity = 0m;
                if (product.TrackingMode == TrackingMode.Batch || product.TrackingMode == TrackingMode.BatchAndSerial)
                {
                    foreach (var b in whBalances)
                    {
                        if (string.IsNullOrWhiteSpace(b.BatchNumber) || b.QuantityOnHand <= 0)
                            continue;

                        if (batchExpiryDict.TryGetValue((product.ProductId, b.BatchNumber), out var expDate) && expDate.HasValue)
                        {
                            if (expDate.Value.Date < effectiveDate)
                            {
                                expiredQuantity += b.QuantityOnHand;
                            }
                        }
                    }
                }

                decimal eligibleOnHand = Math.Max(0m, totalOnHand - expiredQuantity);

                // Calculate inbound supply (QuantityOnOrder)
                // 1. From maintained balance records
                decimal balanceOnOrder = whBalances.Sum(b => b.QuantityOnOrder);

                // 2. From approved PO lines mapped to this branch/warehouse (if not already captured in balances)
                decimal poLineOutstanding = 0m;
                if (warehouse.BranchId.HasValue && warehousesByBranch.TryGetValue(warehouse.BranchId.Value, out var branchWhList))
                {
                    if (branchWhList.Count == 1) // Unambiguous 1:1 branch warehouse
                    {
                        poQtyByBranchAndProduct.TryGetValue((warehouse.BranchId.Value, product.ProductId), out poLineOutstanding);
                    }
                }

                // Count exactly once: take the maximum of balance-maintained or transactional PO supply
                decimal quantityOnOrder = Math.Max(balanceOnOrder, poLineOutstanding);

                // Net Available Planning Stock formula
                decimal netAvailable = eligibleOnHand - totalAllocated - totalQuarantine + quantityOnOrder;

                // Resolve effective planning settings (warehouse override precedence)
                settingsLookup.TryGetValue((product.ProductId, warehouse.WarehouseId), out var overrideSetting);
                var effectiveSettings = product.GetEffectivePlanningSettings(overrideSetting);

                // Trigger rules
                bool isBelowSafety = effectiveSettings.SafetyStockQuantity.HasValue &&
                                     netAvailable <= effectiveSettings.SafetyStockQuantity.Value;

                bool isBelowMin = effectiveSettings.MinStockQuantity.HasValue &&
                                  netAvailable <= effectiveSettings.MinStockQuantity.Value;

                bool isReorderTriggered = false;
                if (effectiveSettings.ReorderLevel > 0)
                {
                    isReorderTriggered = netAvailable <= effectiveSettings.ReorderLevel;
                }
                else if (effectiveSettings.MinStockQuantity.HasValue || effectiveSettings.SafetyStockQuantity.HasValue)
                {
                    isReorderTriggered = isBelowMin || isBelowSafety;
                }
                else
                {
                    // If no threshold is set, trigger only if out of stock
                    isReorderTriggered = netAvailable <= 0m;
                }

                if (isBelowMin || isBelowSafety)
                {
                    isReorderTriggered = true;
                }

                // Urgency classification
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

                // Sizing & Configuration warnings
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

                categories.TryGetValue(product.CategoryId, out var catName);

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
