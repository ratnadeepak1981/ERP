using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class FEFOPickingAndExpiryDispatchTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public FEFOPickingAndExpiryDispatchTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EligibleBatches_AreReturnedInFEFOOrder_EarliestExpiryFirst()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BATCH.VIEW", "PRODUCT.VIEW" });

        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "FEFO_L1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "FEFO_PRD");

        string batchFar = $"LOT-FAR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        string batchNear = $"LOT-NEAR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        string batchMid = $"LOT-MID-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var now = DateTime.UtcNow.Date;
        var expNear = now.AddDays(30);
        var expMid = now.AddDays(90);
        var expFar = now.AddDays(365);

        // Inflow in non-FEFO order: Far, then Near, then Mid
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batchFar, 10m, expFar);
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batchNear, 10m, expNear);
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batchMid, 10m, expMid);

        // Query eligible batches
        var res = await client.GetAsync($"/api/inventory/batches/eligible?productId={product.ProductId}&locationId={loc.WarehouseLocationId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var batches = await res.ReadApiResponseDataAsync<List<EligibleBatchDto>>();
        Assert.NotNull(batches);
        Assert.Equal(3, batches.Count);

        // Must be in strict FEFO sequence: Near (30d), Mid (90d), Far (365d)
        Assert.Equal(batchNear, batches[0].BatchNumber);
        Assert.Equal(batchMid, batches[1].BatchNumber);
        Assert.Equal(batchFar, batches[2].BatchNumber);
    }

    [Fact]
    public async Task QuickDispatchFromOrder_AutomaticallyAllocatesBatchesInFEFOOrder_AndSplitsQuantities()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "FEFO_SPLIT");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "SPLIT_PRD");

        string batch1 = $"B1-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string batch2 = $"B2-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var now = DateTime.UtcNow.Date;
        var exp1 = now.AddDays(20); // Earlier expiry
        var exp2 = now.AddDays(80); // Later expiry

        // Receive 6 into Batch 1 and 10 into Batch 2
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batch1, 6m, exp1);
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batch2, 10m, exp2);

        // Create Sales Order for quantity 10
        var order = await CreateApprovedSalesOrderAsync(db, tenantId, companyId, branchId, product.ProductId, 10m);

        // Quick dispatch: needs 10 units. Should take 6 from Batch 1 and 4 from Batch 2
        var dispatchResult = await shipmentService.CreateAndDispatchFromOrderAsync(
            tenantId,
            companyId,
            branchId,
            order.Id,
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId,
                ShipmentDate = now
            });

        Assert.NotNull(dispatchResult);
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatchResult.Status);
        Assert.Equal(2, dispatchResult.Items.Count);

        // Line 1: 6 units from Batch 1
        var item1 = dispatchResult.Items.FirstOrDefault(i => i.BatchNumber == batch1);
        Assert.NotNull(item1);
        Assert.Equal(6m, item1.QuantityDispatched);

        // Line 2: 4 units from Batch 2
        var item2 = dispatchResult.Items.FirstOrDefault(i => i.BatchNumber == batch2);
        Assert.NotNull(item2);
        Assert.Equal(4m, item2.QuantityDispatched);

        // Verify balances in database
        using var verifyScope = _fixture.Services.CreateScope();
        var vDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var bal1 = await vDb.InventoryBalances.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == loc.WarehouseLocationId && b.BatchNumber == batch1);
        Assert.NotNull(bal1);
        Assert.Equal(0m, bal1.QuantityAvailable);
        Assert.Equal(0m, bal1.QuantityOnHand);

        var bal2 = await vDb.InventoryBalances.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == loc.WarehouseLocationId && b.BatchNumber == batch2);
        Assert.NotNull(bal2);
        Assert.Equal(6m, bal2.QuantityAvailable);
        Assert.Equal(6m, bal2.QuantityOnHand);

        // Verify order fulfillment
        var orderAfter = await vDb.SalesOrders.FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(orderAfter);
        Assert.Equal(SalesOrderStatus.Fulfilled, orderAfter.Status);
    }

    [Fact]
    public async Task MultiBatchSplit_ManualShipment_SucceedsWithoutOverShipping()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "MAN_SPLIT");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "MAN_PRD");

        string b1 = $"MB1-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string b2 = $"MB2-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var now = DateTime.UtcNow.Date;
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, b1, 20m, now.AddDays(30));
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, b2, 20m, now.AddDays(60));

        var order = await CreateApprovedSalesOrderAsync(db, tenantId, companyId, branchId, product.ProductId, 15m);
        var orderItem = order.Items[0];

        // Create shipment draft manually with two lines referencing the same order item: 7 from b1 and 8 from b2 (Total = 15)
        var shipment = await shipmentService.CreateManualDraftAsync(
            tenantId,
            companyId,
            branchId,
            new CreateSalesShipmentRequest
            {
                SalesOrderId = order.Id,
                WarehouseId = wh.WarehouseId,
                ShipmentNumber = $"SH-SPLIT-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new CreateSalesShipmentItemRequest
                    {
                        SalesOrderItemId = orderItem.Id,
                        ProductId = product.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 7m,
                        BatchNumber = b1
                    },
                    new CreateSalesShipmentItemRequest
                    {
                        SalesOrderItemId = orderItem.Id,
                        ProductId = product.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 8m,
                        BatchNumber = b2
                    }
                }
            });

        Assert.Equal(SalesShipmentStatus.Draft, shipment.Status);

        // Dispatch
        var dispatched = await shipmentService.DispatchShipmentAsync(tenantId, companyId, branchId, shipment.Id);
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatched.Status);

        using var verifyScope = _fixture.Services.CreateScope();
        var vDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var orderAfter = await vDb.SalesOrders.FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(orderAfter);
        Assert.Equal(SalesOrderStatus.Fulfilled, orderAfter.Status);
    }

    [Fact]
    public async Task ExpiredBatch_IsExcludedFromEligibleBatches_AndRejectedOnDispatch()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BATCH.VIEW", "PRODUCT.VIEW" });

        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "EXP_LOC");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "EXP_PRD");

        string expiredBatch = $"EXP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string validBatch = $"VAL-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var now = DateTime.UtcNow.Date;
        var expPast = now.AddDays(-10); // Expired 10 days ago
        var expFuture = now.AddDays(90);

        // Inflow expired batch (e.g. historical opening stock) and valid batch
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, expiredBatch, 25m, expPast);
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, validBatch, 15m, expFuture);

        // 1. Check eligible batches endpoint -> Expired batch MUST be excluded
        var res = await client.GetAsync($"/api/inventory/batches/eligible?productId={product.ProductId}&locationId={loc.WarehouseLocationId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var eligible = await res.ReadApiResponseDataAsync<List<EligibleBatchDto>>();
        Assert.NotNull(eligible);
        Assert.Single(eligible);
        Assert.Equal(validBatch, eligible[0].BatchNumber);

        // 2. Attempting to dispatch the expired batch directly must be rejected
        var order = await CreateApprovedSalesOrderAsync(db, tenantId, companyId, branchId, product.ProductId, 5m);
        var orderItem = order.Items[0];

        var shipment = await shipmentService.CreateManualDraftAsync(
            tenantId,
            companyId,
            branchId,
            new CreateSalesShipmentRequest
            {
                SalesOrderId = order.Id,
                WarehouseId = wh.WarehouseId,
                ShipmentNumber = $"SH-EXP-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new CreateSalesShipmentItemRequest
                    {
                        SalesOrderItemId = orderItem.Id,
                        ProductId = product.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 5m,
                        BatchNumber = expiredBatch
                    }
                }
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shipmentService.DispatchShipmentAsync(tenantId, companyId, branchId, shipment.Id));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpiredBatch_IsRejectedOnStockIssue()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "ISS_EXP");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "ISS_PRD");

        string expiredBatch = $"EXP-ISS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var now = DateTime.UtcNow.Date;
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, expiredBatch, 20m, now.AddDays(-5));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockIssueAsync(tenantId, new CreateStockIssueRequest
            {
                WarehouseId = wh.WarehouseId,
                IssueDate = now,
                Purpose = StockIssuePurpose.InternalUse,
                Lines = new List<CreateStockIssueLineRequest>
                {
                    new CreateStockIssueLineRequest
                    {
                        ProductId = product.ProductId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        Quantity = 5m,
                        BatchNumber = expiredBatch
                    }
                }
            }));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonPickableLocation_AndQuarantinedStock_AreExcludedFromEligibleBatches()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BATCH.VIEW", "PRODUCT.VIEW" });

        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);

        // Create Quarantine LocationType (CanPick = false)
        var quarantineType = LocationType.Create(
            tenantId,
            $"QC_{Guid.NewGuid():N}"[..6],
            "Quarantine Quality Inspection",
            canStoreInventory: true,
            canPick: false,
            canPutAway: true);
        await db.LocationTypes.AddAsync(quarantineType);
        await db.SaveChangesAsync();

        // Create Location with Quarantine type
        string qLocCode = $"QLOC_{Guid.NewGuid():N}"[..8];
        var qLoc = WarehouseLocation.Create(
            tenantId,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: qLocCode,
            locationName: "Quarantine Bin",
            locationTypeId: quarantineType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(qLoc);
        await db.SaveChangesAsync();

        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "QC_PRD");
        string batchQ = $"Q-BATCH-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var now = DateTime.UtcNow.Date;

        // Inflow stock into quarantine location
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, qLoc.WarehouseLocationId, product.ProductId, batchQ, 50m, now.AddDays(180));

        // Query eligible batches for picking -> Quarantine location MUST be excluded because CanPick is false
        var res = await client.GetAsync($"/api/inventory/batches/eligible?productId={product.ProductId}&warehouseId={wh.WarehouseId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var eligible = await res.ReadApiResponseDataAsync<List<EligibleBatchDto>>();
        Assert.NotNull(eligible);
        Assert.Empty(eligible); // No eligible pickable stock!
    }

    [Fact]
    public async Task InsufficientEligibleStock_RejectsDispatch_EvenIfExpiredStockExists()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "INSUFF_LOC");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "INSUFF_PRD");

        string expBatch = $"EXPB-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string valBatch = $"VALB-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var now = DateTime.UtcNow.Date;
        // 10 units expired, 5 units valid (total on-hand = 15, but eligible = only 5)
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, expBatch, 10m, now.AddDays(-1));
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, valBatch, 5m, now.AddDays(30));

        // Demand = 8 units (exceeds 5 eligible units)
        var order = await CreateApprovedSalesOrderAsync(db, tenantId, companyId, branchId, product.ProductId, 8m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shipmentService.CreateAndDispatchFromOrderAsync(
                tenantId,
                companyId,
                branchId,
                order.Id,
                new DispatchOrderRequest
                {
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    ShipmentDate = now
                }));

        Assert.Contains("Insufficient eligible stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_RevalidatesAtPostingTime_AndRollsBackOnFailure()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var shipmentService = sp.GetRequiredService<ISalesShipmentService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "RB_LOC");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "RB_PRD");

        string batchNo = $"RB-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var now = DateTime.UtcNow.Date;

        // Inflow 10 units
        await ReceiveStockAsync(postingService, tenantId, wh.WarehouseId, loc.WarehouseLocationId, product.ProductId, batchNo, 10m, now.AddDays(60));

        var order = await CreateApprovedSalesOrderAsync(db, tenantId, companyId, branchId, product.ProductId, 10m);
        var orderItem = order.Items[0];

        // Create shipment draft for 10 units
        var shipment = await shipmentService.CreateManualDraftAsync(
            tenantId,
            companyId,
            branchId,
            new CreateSalesShipmentRequest
            {
                SalesOrderId = order.Id,
                WarehouseId = wh.WarehouseId,
                ShipmentNumber = $"SH-RB-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new CreateSalesShipmentItemRequest
                    {
                        SalesOrderItemId = orderItem.Id,
                        ProductId = product.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 10m,
                        BatchNumber = batchNo
                    }
                }
            });

        // Simulate concurrent expiry or stock depletion: set batch as expired before dispatch
        var batchEntity = await db.ProductBatches.FirstAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        batchEntity.UpdateDates(batchEntity.ManufacturingDate, now.AddDays(-1));
        await db.SaveChangesAsync();

        // Dispatch must fail with exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shipmentService.DispatchShipmentAsync(tenantId, companyId, branchId, shipment.Id));

        // Verify Rollback: Shipment remains in Draft status, no inventory movement was committed
        using var verifyScope = _fixture.Services.CreateScope();
        var vDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var shipmentAfter = await vDb.SalesShipments.FirstOrDefaultAsync(s => s.Id == shipment.Id);
        Assert.NotNull(shipmentAfter);
        Assert.Equal(SalesShipmentStatus.Draft, shipmentAfter.Status);

        var balanceAfter = await vDb.InventoryBalances.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == loc.WarehouseLocationId && b.BatchNumber == batchNo);
        Assert.NotNull(balanceAfter);
        Assert.Equal(10m, balanceAfter.QuantityOnHand);
        Assert.Equal(10m, balanceAfter.QuantityAvailable);
    }

    private static async Task ReceiveStockAsync(
        IInventoryPostingService postingService,
        Guid tenantId,
        Guid warehouseId,
        Guid locationId,
        Guid productId,
        string batchNumber,
        decimal quantity,
        DateTime? expiryDate)
    {
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locationId,
            Quantity = quantity,
            UnitCost = 10m,
            BatchNumber = batchNumber,
            ManufacturingDate = DateTime.UtcNow.AddMonths(-1),
            ExpiryDate = expiryDate
        });
    }

    private static async Task<SalesOrder> CreateApprovedSalesOrderAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid productId,
        decimal quantity)
    {
        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            BranchId = branchId,
            CustomerId = TestConstants.CustomerAlphaId,
            OrderNumber = $"SO-FEFO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            OrderDate = DateTime.UtcNow,
            Status = SalesOrderStatus.Confirmed,
            TotalAmount = quantity * 20m,
            Items = new List<SalesOrderItem>
            {
                new SalesOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = 20m,
                    LineTotal = quantity * 20m,
                    IsActive = true
                }
            }
        };

        await db.SalesOrders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId, Guid? companyId = null, Guid? branchId = null)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..14];
        var wh = Warehouse.Create(
            tenantId,
            code,
            $"Warehouse_{code}",
            city: "Colombo",
            companyId: companyId,
            branchId: branchId);
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId, string codePrefix)
    {
        string code = $"L_{Guid.NewGuid():N}"[..16];
        var loc = WarehouseLocation.Create(
            tenantId,
            warehouseId,
            warehouseZoneId: null,
            locationCode: code,
            locationName: $"Location {code}");
        await db.WarehouseLocations.AddAsync(loc);
        await db.SaveChangesAsync();
        return loc;
    }

    private static async Task<Product> CreateTestProductAsync(DomainDbContext db, Guid tenantId, TrackingMode trackingMode, string prefix = "PRD")
    {
        string code = $"P_{Guid.NewGuid():N}"[..18];
        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            TestConstants.CategoryAlphaId,
            unitOfMeasure: "PCS",
            trackingMode: trackingMode,
            valuationMethod: ValuationMethod.FIFO);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
