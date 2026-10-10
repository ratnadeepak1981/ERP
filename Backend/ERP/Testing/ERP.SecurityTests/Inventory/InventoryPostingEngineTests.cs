using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryPostingEngineTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryPostingEngineTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task InflowAndOutflow_UpdatesLedgerAndBalanceCorrectly()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_01");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        var docId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        // 1. Inflow (+10)
        var inTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = docId,
            SourceDocumentLineId = lineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 50m
        });

        Assert.NotNull(inTxn);
        Assert.Equal(10m, inTxn.Quantity);
        Assert.Equal(500m, inTxn.TotalCost);

        var balanceAfterIn = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balanceAfterIn);
        Assert.Equal(10m, balanceAfterIn.QuantityOnHand);
        Assert.Equal(10m, balanceAfterIn.QuantityAvailable);
        Assert.Equal(0m, balanceAfterIn.QuantityQuarantine);

        // 2. Outflow (-3)
        var outDocId = Guid.NewGuid();
        var outLineId = Guid.NewGuid();
        var outTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = outDocId,
            SourceDocumentLineId = outLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 3m,
            UnitCost = 50m
        });

        Assert.Equal(-3m, outTxn.Quantity);

        var balanceAfterOut = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balanceAfterOut);
        Assert.Equal(7m, balanceAfterOut.QuantityOnHand);
        Assert.Equal(7m, balanceAfterOut.QuantityAvailable);

        // Verify Audit records exist in DomainAuditRecords
        var auditRecords = await db.DomainAuditRecords
            .Where(a => a.TenantId == tenantId && (a.EntityName == nameof(InventoryTransaction) || a.EntityName == nameof(InventoryBalance)))
            .ToListAsync();
        Assert.NotEmpty(auditRecords);
    }

    [Fact]
    public async Task OverdrawAttempt_BeyondAvailable_IsRejected()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_OD");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Inflow of 5 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m
        });

        // Attempt to issue 6 units -> Insufficient stock
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 6m
        }));
    }

    [Fact]
    public async Task QCQuarantine_PreventsIssueAndDoubleCounting()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_QC");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Inflow 10 units marked as Quarantine
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            IsQuarantine = true
        });

        var balance = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(10m, balance.QuantityOnHand);
        Assert.Equal(10m, balance.QuantityQuarantine);
        Assert.Equal(0m, balance.QuantityAvailable); // Available MUST be 0!

        // Attempting normal outbound issue must fail because Available is 0
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 1m
        }));
    }

    [Fact]
    public async Task InternalTransfer_MaintainsStockConservation_AndSupportsQCTransfer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_SRC");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_DST");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // 1. Initial receipt at source (10 units in QC)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            IsQuarantine = true
        });

        // 2. Transfer out of QC source to standard picking destination (QC release transfer)
        var transferDocId = Guid.NewGuid();
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = transferDocId,
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 10m,
            IsSourceQuarantine = true,
            IsDestinationQuarantine = false
        });

        Assert.Equal(-10m, outTxn.Quantity);
        Assert.Equal(10m, inTxn.Quantity);
        Assert.Equal(0m, outTxn.Quantity + inTxn.Quantity); // Net zero company stock change!

        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(0m, srcBal!.QuantityOnHand);
        Assert.Equal(0m, srcBal.QuantityQuarantine);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(10m, dstBal!.QuantityOnHand);
        Assert.Equal(0m, dstBal.QuantityQuarantine);
        Assert.Equal(10m, dstBal.QuantityAvailable); // Now cleanly available for issue
    }

    [Fact]
    public async Task AppendOnlyReversal_CannotReverseMoreThanRemainingQuantity()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REV");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Initial Inflow (+10)
        var origTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m
        });

        // 1. Partial reversal of 4 units
        var rev1 = await postingService.PostReversalAsync(tenantId, new PostReversalRequest
        {
            OriginalTransactionId = origTxn.InventoryTransactionId,
            QuantityToReverse = 4m
        });

        Assert.Equal(-4m, rev1.Quantity);
        Assert.Equal(origTxn.InventoryTransactionId, rev1.ReversalOfTransactionId);

        var bal1 = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(6m, bal1!.QuantityOnHand);

        // 2. Second partial reversal of 6 units (reverses remaining quantity)
        var rev2 = await postingService.PostReversalAsync(tenantId, new PostReversalRequest
        {
            OriginalTransactionId = origTxn.InventoryTransactionId,
            QuantityToReverse = 6m
        });

        Assert.Equal(-6m, rev2.Quantity);
        var bal2 = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(0m, bal2!.QuantityOnHand);

        // 3. Attempting further reversal must be blocked
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostReversalAsync(tenantId, new PostReversalRequest
        {
            OriginalTransactionId = origTxn.InventoryTransactionId,
            QuantityToReverse = 1m
        }));
    }

    [Fact]
    public async Task Idempotency_DuplicatePostingKey_IsRejected()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_IDEM");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

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
            Quantity = 10m
        };

        // First post succeeds
        await postingService.PostMovementAsync(tenantId, req);

        // Duplicate post is rejected by idempotency check
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, req));
    }

    [Fact]
    public async Task NonStorableLocation_RejectsInventoryPosting()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);

        // Create location type with CanStoreInventory = false (e.g. Aisle)
        var nonStorableType = LocationType.Create(
            tenantId,
            $"AISLE_{Guid.NewGuid():N}"[..8],
            "Aisle Grouping",
            canStoreInventory: false,
            canPick: false,
            canPutAway: false,
            canContainChildren: true);
        await db.LocationTypes.AddAsync(nonStorableType);
        await db.SaveChangesAsync();

        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "AISLE_01", nonStorableType.LocationTypeId);
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m
        }));
    }

    [Fact]
    public async Task BatchAndSerialTracking_EnforcesProductTrackingMode()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_TRACK");

        // 1. Batch-tracked product requires batch
        var batchProduct = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = batchProduct.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m
            // BatchNumber omitted
        }));

        // Supplying batch succeeds
        var batchTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = batchProduct.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            BatchNumber = "BATCH-2026-A"
        });
        Assert.Equal("BATCH-2026-A", batchTxn.BatchNumber);

        // 2. Serial-tracked product requires serial and Qty of 1
        var serialProduct = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial);
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = serialProduct.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 5m, // Serial items must have Qty = 1
            SerialNumber = "SN-001"
        }));

        var serialTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = serialProduct.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 1m,
            SerialNumber = "SN-001"
        });
        Assert.Equal("SN-001", serialTxn.SerialNumber);
    }

    [Fact]
    public async Task TenantIsolation_StrictlyPartitionsBalancesAndLedger()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantA = TestConstants.TenantAlphaId;
        var tenantB = TestConstants.TenantBetaId;

        var whA = await CreateTestWarehouseAsync(db, tenantA);
        var locA = await CreateTestLocationAsync(db, tenantA, whA.WarehouseId, "BIN_T1");
        var prodA = await CreateTestProductAsync(db, tenantA, TrackingMode.None);

        await postingService.PostMovementAsync(tenantA, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = prodA.ProductId,
            WarehouseId = whA.WarehouseId,
            WarehouseLocationId = locA.WarehouseLocationId,
            Quantity = 20m
        });

        // Tenant B cannot view Tenant A balance
        var balTenantB = await postingService.GetBalanceAsync(tenantB, locA.WarehouseLocationId, prodA.ProductId);
        Assert.Null(balTenantB);

        // Tenant B attempting to post using Tenant A product is rejected
        await Assert.ThrowsAsync<KeyNotFoundException>(() => postingService.PostMovementAsync(tenantB, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = prodA.ProductId,
            WarehouseId = whA.WarehouseId,
            WarehouseLocationId = locA.WarehouseLocationId,
            Quantity = 5m
        }));
    }

    // Helper setup methods
    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..8];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", city: "Colombo");
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid warehouseId,
        string prefix,
        Guid? locationTypeId = null)
    {
        string code = $"{prefix}_{Guid.NewGuid():N}"[..8];
        var loc = WarehouseLocation.Create(
            tenantId,
            warehouseId,
            warehouseZoneId: null,
            locationCode: code,
            locationName: $"Location {code}",
            locationTypeId: locationTypeId);
        await db.WarehouseLocations.AddAsync(loc);
        await db.SaveChangesAsync();
        return loc;
    }

    private static async Task<Product> CreateTestProductAsync(
        DomainDbContext db,
        Guid tenantId,
        TrackingMode trackingMode)
    {
        string code = $"PRD_{Guid.NewGuid():N}"[..8];
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        Guid catId = cat?.CategoryId ?? Guid.NewGuid();

        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            catId,
            unitOfMeasure: "PCS",
            trackingMode: trackingMode,
            isStockTracked: true);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
