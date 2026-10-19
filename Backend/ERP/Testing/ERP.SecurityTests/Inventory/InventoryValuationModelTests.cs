using System;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryValuationModelTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryValuationModelTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public void InventoryCostLayer_PartialAndFinalConsumption_AbsorbsResidualCentsCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var txnId = Guid.NewGuid();

        // 3 units at 33.33 each = 99.99 total
        var layer = InventoryCostLayer.Create(
            tenantId,
            productId,
            warehouseId,
            locationId,
            txnId,
            DateTime.UtcNow,
            3m,
            33.33m);

        Assert.Equal(3m, layer.OriginalQuantity);
        Assert.Equal(3m, layer.RemainingQuantity);
        Assert.Equal(99.99m, layer.OriginalCost);
        Assert.Equal(99.99m, layer.RemainingCost);
        Assert.False(layer.IsExhausted);

        // 1. First partial issue of 1 unit
        decimal issueCost1 = layer.Consume(1m);
        Assert.Equal(33.33m, issueCost1);
        Assert.Equal(2m, layer.RemainingQuantity);
        Assert.Equal(66.66m, layer.RemainingCost);
        Assert.False(layer.IsExhausted);

        // 2. Second partial issue of 1 unit
        decimal issueCost2 = layer.Consume(1m);
        Assert.Equal(33.33m, issueCost2);
        Assert.Equal(1m, layer.RemainingQuantity);
        Assert.Equal(33.33m, layer.RemainingCost);
        Assert.False(layer.IsExhausted);

        // 3. Final depletion of remaining 1 unit
        decimal finalIssueCost = layer.Consume(1m);
        Assert.Equal(33.33m, finalIssueCost);
        Assert.Equal(0m, layer.RemainingQuantity);
        Assert.Equal(0m, layer.RemainingCost);
        Assert.True(layer.IsExhausted);

        // Total issue costs must match original cost exactly
        Assert.Equal(layer.OriginalCost, issueCost1 + issueCost2 + finalIssueCost);

        // Attempting to consume from exhausted layer throws
        Assert.Throws<InvalidOperationException>(() => layer.Consume(1m));
    }

    [Fact]
    public void InventoryCostLayer_DeductForReversal_RejectsIfStockAlreadyConsumed()
    {
        var layer = InventoryCostLayer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            10m,
            50m);

        // Consume 8 units
        layer.Consume(8m);
        Assert.Equal(2m, layer.RemainingQuantity);

        // Attempting to reverse 5 units from original receipt must fail because only 2 remain
        var ex = Assert.Throws<InvalidOperationException>(() => layer.DeductForReversal(5m));
        Assert.Contains("only 2 unconsumed units remain", ex.Message);

        // Reversing 2 units succeeds and exhausts the layer
        decimal deducted = layer.DeductForReversal(2m);
        Assert.Equal(100m, deducted);
        Assert.Equal(0m, layer.RemainingQuantity);
        Assert.Equal(0m, layer.RemainingCost);
        Assert.True(layer.IsExhausted);
    }

    [Fact]
    public void ProductValuationBalance_MovingWeightedAverage_CalculatesAccuratelyAndZerosCleanly()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var balance = ProductValuationBalance.Create(tenantId, productId, warehouseId);
        Assert.Equal(0m, balance.TotalQuantity);
        Assert.Equal(0m, balance.TotalCostValue);
        Assert.Equal(0m, balance.CurrentAverageCost);

        // 1. First Inflow: 10 units @ 100.00
        balance.ApplyInflow(10m, 100m);
        Assert.Equal(10m, balance.TotalQuantity);
        Assert.Equal(1000m, balance.TotalCostValue);
        Assert.Equal(100m, balance.CurrentAverageCost);

        // 2. Second Inflow: 10 units @ 150.00 -> Total Value: 2500 on 20 units -> Avg: 125.00
        balance.ApplyInflow(10m, 150m);
        Assert.Equal(20m, balance.TotalQuantity);
        Assert.Equal(2500m, balance.TotalCostValue);
        Assert.Equal(125m, balance.CurrentAverageCost);

        // 3. Partial Outflow: 5 units @ 125.00 = 625.00
        decimal issue1 = balance.ApplyOutflow(5m);
        Assert.Equal(625m, issue1);
        Assert.Equal(15m, balance.TotalQuantity);
        Assert.Equal(1875m, balance.TotalCostValue);
        Assert.Equal(125m, balance.CurrentAverageCost);

        // 4. Full Outflow of remaining 15 units
        decimal issueFinal = balance.ApplyOutflow(15m);
        Assert.Equal(1875m, issueFinal);
        Assert.Equal(0m, balance.TotalQuantity);
        Assert.Equal(0m, balance.TotalCostValue);

        // 5. Outflow beyond available quantity throws
        Assert.Throws<InvalidOperationException>(() => balance.ApplyOutflow(1m));
    }

    [Fact]
    public async Task InventoryCostLayerAndValuationBalance_PersistInDatabaseSuccessfully()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // 1. Create product, warehouse, location, and dummy transaction
        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"VAL-PROD-{suffix}",
            $"Valuation Test Product {suffix}",
            category.CategoryId,
            valuationMethod: ValuationMethod.FIFO);
        await db.Products.AddAsync(product);

        var warehouse = Warehouse.Create(
            tenantId,
            $"W-VAL-{suffix}",
            $"Valuation Warehouse {suffix}");
        await db.Warehouses.AddAsync(warehouse);

        var location = WarehouseLocation.Create(
            tenantId,
            warehouse.WarehouseId,
            null,
            $"LOC-VAL-{suffix}",
            $"Location {suffix}");
        await db.WarehouseLocations.AddAsync(location);

        var txn = InventoryTransaction.Create(
            tenantId,
            $"TXN-VAL-{suffix}",
            InventoryMovementType.GoodsReceipt,
            SourceDocumentType.GoodsReceiptNote,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            product.ProductId,
            warehouse.WarehouseId,
            location.WarehouseLocationId,
            10m,
            25m);
        await db.InventoryTransactions.AddAsync(txn);

        // 2. Create Cost Layer
        var layer = InventoryCostLayer.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            location.WarehouseLocationId,
            txn.InventoryTransactionId,
            DateTime.UtcNow,
            10m,
            25m,
            batchNumber: $"BATCH-{suffix}");
        await db.InventoryCostLayers.AddAsync(layer);

        // 3. Create Valuation Balance
        var valBalance = ProductValuationBalance.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId);
        valBalance.ApplyInflow(10m, 25m);
        await db.ProductValuationBalances.AddAsync(valBalance);

        await db.SaveChangesAsync();

        // 4. Verify in separate query
        var retrievedLayer = await db.InventoryCostLayers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.CostLayerId == layer.CostLayerId);
        Assert.NotNull(retrievedLayer);
        Assert.Equal(10m, retrievedLayer.OriginalQuantity);
        Assert.Equal(25m, retrievedLayer.UnitCost);
        Assert.Equal(250m, retrievedLayer.RemainingCost);
        Assert.Equal($"BATCH-{suffix}", retrievedLayer.BatchNumber);

        var retrievedBalance = await db.ProductValuationBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == warehouse.WarehouseId);
        Assert.NotNull(retrievedBalance);
        Assert.Equal(10m, retrievedBalance.TotalQuantity);
        Assert.Equal(250m, retrievedBalance.TotalCostValue);
        Assert.Equal(25m, retrievedBalance.CurrentAverageCost);
    }
}
