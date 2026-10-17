using System;
using System.Collections.Generic;
using System.Linq;
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

public class InventoryValuationPostingIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryValuationPostingIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task FIFO_ReceiptAndMultiLayerIssue_ConsumesOldestFirstWithCorrectCosts()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.FIFO);

        var docId = Guid.NewGuid();

        // 1. Inflow 1: 10 units @ 10.00
        var inTxn1 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m
        });
        Assert.Equal(10m, inTxn1.UnitCost);
        Assert.Equal(100m, inTxn1.TotalCost);

        // 2. Inflow 2: 10 units @ 20.00
        var inTxn2 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m
        });
        Assert.Equal(20m, inTxn2.UnitCost);
        Assert.Equal(200m, inTxn2.TotalCost);

        // 3. Issue 1: 5 units -> Consumes partially from Layer 1 @ 10.00
        var outTxn1 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionIssue,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m
        });
        Assert.Equal(10m, outTxn1.UnitCost);
        Assert.Equal(50m, outTxn1.TotalCost);

        // 4. Issue 2: 8 units -> Consumes remaining 5 units from Layer 1 (@ 10.00) + 3 units from Layer 2 (@ 20.00)
        // Total cost = 5*10 + 3*20 = 50 + 60 = 110.00. Effective unit cost = 110 / 8 = 13.7500.
        var outTxn2 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionIssue,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 8m
        });
        Assert.Equal(13.75m, outTxn2.UnitCost);
        Assert.Equal(110m, outTxn2.TotalCost);

        // Verify cost layer states in DB
        var layers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .OrderBy(l => l.LayerDate).ThenBy(l => l.CostLayerId)
            .ToListAsync();

        Assert.Equal(2, layers.Count);

        // Layer 1 must be fully exhausted
        Assert.True(layers[0].IsExhausted);
        Assert.Equal(0m, layers[0].RemainingQuantity);
        Assert.Equal(0m, layers[0].RemainingCost);

        // Layer 2 has 7 remaining @ 20.00 = 140.00
        Assert.False(layers[1].IsExhausted);
        Assert.Equal(7m, layers[1].RemainingQuantity);
        Assert.Equal(140m, layers[1].RemainingCost);

        // Physical on-hand balance must be 7
        var balance = await db.InventoryBalances
            .FirstAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseLocationId == location.WarehouseLocationId);
        Assert.Equal(7m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task WeightedAverage_ReceiptsAndIssues_MaintainsMovingAverageAndBalances()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.WeightedAverage);

        var docId = Guid.NewGuid();

        // 1. Receipt 1: 10 units @ 100.00 -> Total Value: 1000.00, Avg: 100.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 100m
        });

        // 2. Receipt 2: 10 units @ 150.00 -> Total Value: 2500.00 on 20 units -> Avg: 125.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 150m
        });

        // 3. Issue: 5 units -> Cost must be at current moving average (125.00) = 625.00
        var outTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionIssue,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m
        });
        Assert.Equal(125m, outTxn.UnitCost);
        Assert.Equal(625m, outTxn.TotalCost);

        // Verify ProductValuationBalance in DB
        var valBalance = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        Assert.Equal(15m, valBalance.TotalQuantity);
        Assert.Equal(1875m, valBalance.TotalCostValue);
        Assert.Equal(125m, valBalance.CurrentAverageCost);
    }

    [Fact]
    public async Task StandardCost_InflowAndOutflow_UsesConfiguredStandardCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 50m);

        // Inflow of 10 units @ supplier cost 65.00 (Standard is 50.00)
        var inTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 65m
        });

        // Inventory is recorded at Standard Cost (50.00)
        Assert.Equal(50m, inTxn.UnitCost);
        Assert.Equal(500m, inTxn.TotalCost);

        // Outflow of 4 units is relieved at Standard Cost (50.00)
        var outTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionIssue,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 4m
        });

        Assert.Equal(50m, outTxn.UnitCost);
        Assert.Equal(200m, outTxn.TotalCost);
    }

    [Fact]
    public async Task StandardCost_MissingStandardCost_ThrowsException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        // Standard cost is 0
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.GoodsReceipt,
                SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = warehouse.WarehouseId,
                WarehouseLocationId = location.WarehouseLocationId,
                Quantity = 10m,
                UnitCost = 25m
            }));

        Assert.Contains("no valid StandardCost (> 0) is configured", ex.Message);
    }

    [Fact]
    public async Task LIFO_DisabledByDefault_ThrowsNotSupportedException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.LIFO);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.GoodsReceipt,
                SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = warehouse.WarehouseId,
                WarehouseLocationId = location.WarehouseLocationId,
                Quantity = 10m,
                UnitCost = 25m
            }));

        Assert.Contains("prohibited under IFRS and is disabled by default", ex.Message);
    }

    [Fact]
    public async Task InsufficientStockAndNegativeStock_RejectedCleanly()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.FIFO);

        // Attempting to issue stock with 0 on-hand throws
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.ProductionIssue,
                SourceDocumentType = SourceDocumentType.ProductionOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = warehouse.WarehouseId,
                WarehouseLocationId = location.WarehouseLocationId,
                Quantity = 10m
            }));

        Assert.Contains("Insufficient available inventory", ex.Message);
    }

    [Fact]
    public async Task TransferValuationConsistency_InterWarehouseTransferPreservesValue()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"PROD-TX-{suffix}",
            $"Transfer Test Product {suffix}",
            category.CategoryId,
            valuationMethod: ValuationMethod.FIFO);
        await db.Products.AddAsync(product);

        var w1 = Warehouse.Create(tenantId, $"W1-{suffix}", $"Warehouse 1 {suffix}");
        var w2 = Warehouse.Create(tenantId, $"W2-{suffix}", $"Warehouse 2 {suffix}");
        await db.Warehouses.AddRangeAsync(w1, w2);

        var loc1 = WarehouseLocation.Create(tenantId, w1.WarehouseId, null, $"L1-{suffix}", $"Location 1 {suffix}");
        var loc2 = WarehouseLocation.Create(tenantId, w2.WarehouseId, null, $"L2-{suffix}", $"Location 2 {suffix}");
        await db.WarehouseLocations.AddRangeAsync(loc1, loc2);
        await db.SaveChangesAsync();

        // 1. Receive 10 units @ 42.50 in Warehouse 1
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = w1.WarehouseId,
            WarehouseLocationId = loc1.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 42.50m
        });

        // 2. Transfer 4 units from W1 to W2
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = w1.WarehouseId,
            SourceLocationId = loc1.WarehouseLocationId,
            DestinationWarehouseId = w2.WarehouseId,
            DestinationLocationId = loc2.WarehouseLocationId,
            Quantity = 4m
        });

        // Both transactions must record the exact same transferred cost
        Assert.Equal(42.50m, outTxn.UnitCost);
        Assert.Equal(42.50m, inTxn.UnitCost);
        Assert.Equal(170m, Math.Abs(outTxn.TotalCost));
        Assert.Equal(170m, inTxn.TotalCost);

        // Destination W2 must now have a valid cost layer of 4 units @ 42.50
        var w2Layers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.WarehouseId == w2.WarehouseId)
            .ToListAsync();

        Assert.Single(w2Layers);
        Assert.Equal(4m, w2Layers[0].RemainingQuantity);
        Assert.Equal(42.50m, w2Layers[0].UnitCost);
        Assert.Equal(170m, w2Layers[0].RemainingCost);
    }

    [Fact]
    public async Task Reversal_FIFO_InflowAndOutflowReversalHandledCorrectly()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.FIFO);

        // 1. Receive 10 units @ 30.00
        var receiptTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 30m
        });

        // 2. Issue 4 units @ 30.00
        var issueTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionIssue,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 4m
        });

        // 3. Reverse the issue (Customer/Production return): Restores 4 units at historical cost 30.00
        var reverseIssueTxn = await postingService.PostReversalAsync(tenantId, new PostReversalRequest
        {
            OriginalTransactionId = issueTxn.InventoryTransactionId,
            QuantityToReverse = 4m
        });

        Assert.Equal(30m, reverseIssueTxn.UnitCost);
        Assert.Equal(120m, reverseIssueTxn.TotalCost);

        // 4. Attempting to reverse 10 units of original receipt must fail because 4 units were issued
        // (even though 4 were restored, the original receipt layer only has 6 unconsumed units)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostReversalAsync(tenantId, new PostReversalRequest
            {
                OriginalTransactionId = receiptTxn.InventoryTransactionId,
                QuantityToReverse = 10m
            }));

        Assert.Contains("only 6 unconsumed units remain in this receipt layer", ex.Message);
    }

    [Fact]
    public async Task DuplicatePostingRetries_PreventedByIdempotencyKey()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, location) = await CreateTestSetupAsync(db, tenantId, ValuationMethod.FIFO);

        var docId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var req = new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = lineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 15m
        };

        // First post succeeds
        await postingService.PostMovementAsync(tenantId, req);

        // Retry with identical idempotency key throws
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostMovementAsync(tenantId, req));

        Assert.Contains("Duplicate inventory movement posting detected", ex.Message);
    }

    [Fact]
    public async Task CrossTenantIsolation_RejectsAccessToForeignTenantProduct()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        // Product belongs to Tenant Alpha
        var (product, warehouse, location) = await CreateTestSetupAsync(db, TestConstants.TenantAlphaId, ValuationMethod.FIFO);

        // Tenant Beta attempts to post against Tenant Alpha's product
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            postingService.PostMovementAsync(TestConstants.TenantBetaId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.GoodsReceipt,
                SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = product.ProductId,
                WarehouseId = warehouse.WarehouseId,
                WarehouseLocationId = location.WarehouseLocationId,
                Quantity = 5m,
                UnitCost = 15m
            }));
    }

    private static async Task<(Product Product, Warehouse Warehouse, WarehouseLocation Location)> CreateTestSetupAsync(
        DomainDbContext db,
        Guid tenantId,
        ValuationMethod valuationMethod,
        decimal standardCost = 0)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"VAL-P-{suffix}",
            $"Valuation Product {suffix}",
            category.CategoryId,
            valuationMethod: valuationMethod,
            standardCost: standardCost);
        await db.Products.AddAsync(product);

        var warehouse = Warehouse.Create(
            tenantId,
            $"VAL-W-{suffix}",
            $"Valuation Warehouse {suffix}");
        await db.Warehouses.AddAsync(warehouse);

        var location = WarehouseLocation.Create(
            tenantId,
            warehouse.WarehouseId,
            null,
            $"VAL-L-{suffix}",
            $"Valuation Location {suffix}");
        await db.WarehouseLocations.AddAsync(location);

        await db.SaveChangesAsync();

        return (product, warehouse, location);
    }
}
