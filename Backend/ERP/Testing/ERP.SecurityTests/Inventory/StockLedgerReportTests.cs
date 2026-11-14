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

public class StockLedgerReportTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockLedgerReportTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task GetStockLedgerReport_ChronologicalMovements_CalculatesRunningQuantitiesAndValuesCorrectly()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // 1. Setup Master Data
        var wh = Warehouse.Create(tenantId, $"WH_{suffix}", $"Warehouse_{suffix}", city: "Colombo");
        await db.Warehouses.AddAsync(wh);

        var binType = LocationType.Create(tenantId, $"BIN_{suffix}", "Storage Bin", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddAsync(binType);

        var loc = WarehouseLocation.Create(
            tenantId,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: $"LOC_{suffix}",
            locationName: $"Bin_{suffix}",
            locationTypeId: binType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(loc);

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"LED-P-{suffix}",
            $"Ledger Product {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.FIFO,
            standardCost: 50.00m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // 2. Post Movements:
        // Movement 1: Goods Receipt +100 units @ 50.00
        var receiptDocId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = receiptDocId,
            SourceDocumentLineId = receiptLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 100m,
            UnitCost = 50.00m
        });

        // Movement 2: Stock Adjustment Out -30 units @ 50.00
        var adjOutDocId = Guid.NewGuid();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentOut,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = adjOutDocId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = -30m,
            UnitCost = 50.00m
        });

        // Movement 3: Adjustment In +20 units @ 50.00
        var adjInDocId = Guid.NewGuid();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentIn,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = adjInDocId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 50.00m
        });

        // 3. Query Stock Ledger
        var report = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId
        });

        // 4. Assert Authoritative Quantities and Values
        Assert.NotNull(report);
        Assert.Equal(product.ProductId, report.ProductId);
        Assert.Equal(product.ProductCode, report.ProductCode);
        Assert.Equal(wh.WarehouseId, report.WarehouseId);
        Assert.Equal(loc.WarehouseLocationId, report.WarehouseLocationId);

        // Opening Balance
        Assert.Equal(0m, report.OpeningQuantity);
        Assert.Equal(0m, report.OpeningValue);

        // Period Movements
        Assert.Equal(120m, report.TotalQuantityIn);
        Assert.Equal(6000m, report.TotalInwardValue);
        Assert.Equal(30m, report.TotalQuantityOut);
        Assert.Equal(1500m, report.TotalOutwardValue);

        // Closing Balance
        Assert.Equal(90m, report.ClosingQuantity);
        Assert.Equal(4500m, report.ClosingValue);

        // Reconciliation Validation: Opening + In - Out == Closing
        Assert.Equal(report.OpeningQuantity + report.TotalQuantityIn - report.TotalQuantityOut, report.ClosingQuantity);
        Assert.Equal(report.OpeningValue + report.TotalInwardValue - report.TotalOutwardValue, report.ClosingValue);

        // Detail Rows
        Assert.Equal(3, report.Items.Count);

        // Row 1: Receipt
        var row1 = report.Items[0];
        Assert.Equal("Goods Receipt (GRN)", row1.TransactionType);
        Assert.Equal(100m, row1.QuantityIn);
        Assert.Equal(0m, row1.QuantityOut);
        Assert.Equal(50.00m, row1.CostRate);
        Assert.Equal(5000m, row1.InwardValue);
        Assert.Equal(100m, row1.RunningQuantity);
        Assert.Equal(5000m, row1.RunningValue);

        // Row 2: Adjustment Out
        var row2 = report.Items[1];
        Assert.Equal("Stock Adjustment (-)", row2.TransactionType);
        Assert.Equal(0m, row2.QuantityIn);
        Assert.Equal(30m, row2.QuantityOut);
        Assert.Equal(50.00m, row2.CostRate);
        Assert.Equal(1500m, row2.OutwardValue);
        Assert.Equal(70m, row2.RunningQuantity);
        Assert.Equal(3500m, row2.RunningValue);

        // Row 3: Adjustment In
        var row3 = report.Items[2];
        Assert.Equal("Stock Adjustment (+)", row3.TransactionType);
        Assert.Equal(20m, row3.QuantityIn);
        Assert.Equal(0m, row3.QuantityOut);
        Assert.Equal(50.00m, row3.CostRate);
        Assert.Equal(1000m, row3.InwardValue);
        Assert.Equal(90m, row3.RunningQuantity);
        Assert.Equal(4500m, row3.RunningValue);
    }

    [Fact]
    public async Task GetStockLedgerReport_DateFromFilter_CalculatesOpeningBalanceFromPriorTransactions()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var wh = Warehouse.Create(tenantId, $"WH_{suffix}", $"Warehouse_{suffix}", city: "Kandy");
        await db.Warehouses.AddAsync(wh);

        var binType = LocationType.Create(tenantId, $"BIN_{suffix}", "Storage Bin", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddAsync(binType);

        var loc = WarehouseLocation.Create(
            tenantId,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: $"LOC_{suffix}",
            locationName: $"Bin_{suffix}",
            locationTypeId: binType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(loc);

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"LED-P2-{suffix}",
            $"Ledger Product 2 {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.StandardCost,
            standardCost: 40.00m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // Transaction 1: Initial Receipt
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 40.00m
        });

        // Delay slightly so timestamp advances
        await Task.Delay(50);
        var cutOffDate = DateTime.UtcNow;
        await Task.Delay(50);

        // Transaction 2: Later Receipt
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.ProductionReceipt,
            SourceDocumentType = SourceDocumentType.ProductionOrder,
            SourceDocumentId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 30m,
            UnitCost = 40.00m
        });

        // Query with DateFrom = cutOffDate
        var report = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            DateFrom = cutOffDate
        });

        Assert.NotNull(report);
        // Opening balance contains Transaction 1
        Assert.Equal(50m, report.OpeningQuantity);
        Assert.Equal(2000m, report.OpeningValue);

        // Period movements contains Transaction 2
        Assert.Equal(30m, report.TotalQuantityIn);
        Assert.Equal(1200m, report.TotalInwardValue);
        Assert.Equal(0m, report.TotalQuantityOut);
        Assert.Equal(0m, report.TotalOutwardValue);

        // Closing balance
        Assert.Equal(80m, report.ClosingQuantity);
        Assert.Equal(3200m, report.ClosingValue);

        // Items only has the 1 period transaction
        Assert.Single(report.Items);
        Assert.Equal(30m, report.Items[0].QuantityIn);
        Assert.Equal(80m, report.Items[0].RunningQuantity);
        Assert.Equal(3200m, report.Items[0].RunningValue);
    }

    [Fact]
    public async Task GetStockLedgerReport_TransferMovements_ResolvesSourceAndDestinationLocations()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var wh = Warehouse.Create(tenantId, $"WH_{suffix}", $"Warehouse_{suffix}", city: "Galle");
        await db.Warehouses.AddAsync(wh);

        var stagingType = LocationType.Create(tenantId, $"STG_{suffix}", "Staging Dock", canStoreInventory: true, canPutAway: false);
        var storageType = LocationType.Create(tenantId, $"STR_{suffix}", "Storage Bin", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddRangeAsync(stagingType, storageType);

        var stagingLoc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, $"STG_{suffix}", $"Dock_{suffix}", stagingType.LocationTypeId);
        var storageLoc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, $"STR_{suffix}", $"Bin_{suffix}", storageType.LocationTypeId);
        await db.WarehouseLocations.AddRangeAsync(stagingLoc, storageLoc);

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"LED-P3-{suffix}",
            $"Ledger Product 3 {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.StandardCost,
            standardCost: 100.00m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // 1. Initial Receipt at Staging
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 25m,
            UnitCost = 100.00m
        });

        // 2. Put Away Transfer from Staging to Storage
        var putawayDocId = Guid.NewGuid();
        var putawayLineId = Guid.NewGuid();
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = putawayDocId,
            SourceDocumentLineId = putawayLineId,
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            Quantity = 25m,
            UnitCost = 100.00m
        });

        // Query Storage Location Stock Ledger
        var storageReport = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId
        });

        Assert.Single(storageReport.Items);
        var storageItem = storageReport.Items[0];
        Assert.Equal("Transfer In", storageItem.TransactionType);
        Assert.Equal(25m, storageItem.QuantityIn);
        Assert.Equal(stagingLoc.LocationCode, storageItem.SourceLocationCode);
        Assert.Equal(storageLoc.LocationCode, storageItem.DestinationLocationCode);
        Assert.Equal(25m, storageReport.ClosingQuantity);
        Assert.Equal(2500m, storageReport.ClosingValue);

        // Query Staging Location Stock Ledger
        var stagingReport = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId
        });

        Assert.Equal(2, stagingReport.Items.Count);
        var transferOutItem = stagingReport.Items.First(i => i.TransactionType == "Transfer Out");
        Assert.Equal(25m, transferOutItem.QuantityOut);
        Assert.Equal(stagingLoc.LocationCode, transferOutItem.SourceLocationCode);
        Assert.Equal(storageLoc.LocationCode, transferOutItem.DestinationLocationCode);
        Assert.Equal(0m, stagingReport.ClosingQuantity);
        Assert.Equal(0m, stagingReport.ClosingValue);
    }

    [Fact]
    public async Task GetStockLedgerReport_AllLocations_ReturnsAllLocationsMovementsWithoutDoubleCounting()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var wh = Warehouse.Create(tenantId, $"WH_{suffix}", $"Warehouse_{suffix}", city: "Kandy");
        await db.Warehouses.AddAsync(wh);

        var stagingType = LocationType.Create(tenantId, $"STG_{suffix}", "Staging Dock", canStoreInventory: true, canPutAway: false);
        var storageType = LocationType.Create(tenantId, $"STR_{suffix}", "Storage Bin", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddRangeAsync(stagingType, storageType);

        var stagingLoc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, $"STG_{suffix}", $"Dock_{suffix}", stagingType.LocationTypeId);
        var storageLoc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, $"STR_{suffix}", $"Bin_{suffix}", storageType.LocationTypeId);
        await db.WarehouseLocations.AddRangeAsync(stagingLoc, storageLoc);

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"LED-ALL-{suffix}",
            $"Ledger Product All {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.StandardCost,
            standardCost: 80.00m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // 1. Initial Receipt at Staging: 50 units @ 80.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 80.00m
        });

        // 2. Put Away Transfer from Staging to Storage: 50 units @ 80.00
        var putawayDocId = Guid.NewGuid();
        var putawayLineId = Guid.NewGuid();
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = putawayDocId,
            SourceDocumentLineId = putawayLineId,
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 80.00m
        });

        // 3. Query Stock Ledger with All Locations (WarehouseLocationId = null)
        var allLocationsReport = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = null // All Locations
        });

        // Verify Header Metadata
        Assert.NotNull(allLocationsReport);
        Assert.Null(allLocationsReport.WarehouseLocationId);
        Assert.Equal("ALL", allLocationsReport.LocationCode);
        Assert.Equal("All Locations", allLocationsReport.LocationName);
        Assert.Contains("All Locations", allLocationsReport.HierarchyPath);

        // Verify Balances & Reconciliation across warehouse
        Assert.Equal(0m, allLocationsReport.OpeningQuantity);
        Assert.Equal(0m, allLocationsReport.OpeningValue);
        Assert.Equal(100m, allLocationsReport.TotalQuantityIn);  // 50 (GRN) + 50 (Transfer In)
        Assert.Equal(8000m, allLocationsReport.TotalInwardValue);
        Assert.Equal(50m, allLocationsReport.TotalQuantityOut);   // 50 (Transfer Out)
        Assert.Equal(4000m, allLocationsReport.TotalOutwardValue);
        Assert.Equal(50m, allLocationsReport.ClosingQuantity);   // Net warehouse stock: 50 units
        Assert.Equal(4000m, allLocationsReport.ClosingValue);    // Net warehouse value: 4000.00
        Assert.Equal(allLocationsReport.OpeningQuantity + allLocationsReport.TotalQuantityIn - allLocationsReport.TotalQuantityOut, allLocationsReport.ClosingQuantity);
        Assert.Equal(allLocationsReport.OpeningValue + allLocationsReport.TotalInwardValue - allLocationsReport.TotalOutwardValue, allLocationsReport.ClosingValue);

        // Verify Rows (3 total rows: GRN at staging, Transfer Out at staging, Transfer In at storage)
        Assert.Equal(3, allLocationsReport.Items.Count);
        var grnRow = allLocationsReport.Items.First(i => i.TransactionType == "Goods Receipt (GRN)");
        Assert.Equal(stagingLoc.LocationCode, grnRow.LocationCode);
        Assert.Equal(50m, grnRow.QuantityIn);

        var xferOutRow = allLocationsReport.Items.First(i => i.TransactionType == "Transfer Out");
        Assert.Equal(stagingLoc.LocationCode, xferOutRow.LocationCode);
        Assert.Equal(50m, xferOutRow.QuantityOut);
        Assert.Equal(stagingLoc.LocationCode, xferOutRow.SourceLocationCode);
        Assert.Equal(storageLoc.LocationCode, xferOutRow.DestinationLocationCode);

        var xferInRow = allLocationsReport.Items.First(i => i.TransactionType == "Transfer In");
        Assert.Equal(storageLoc.LocationCode, xferInRow.LocationCode);
        Assert.Equal(50m, xferInRow.QuantityIn);
        Assert.Equal(stagingLoc.LocationCode, xferInRow.SourceLocationCode);
        Assert.Equal(storageLoc.LocationCode, xferInRow.DestinationLocationCode);

        // 4. Query Individual Location Reports to test warehouse reconciliation
        var stagingReport = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId
        });
        Assert.Equal(0m, stagingReport.ClosingQuantity);
        Assert.Equal(0m, stagingReport.ClosingValue);

        var storageReport = await inventoryService.GetStockLedgerReportAsync(tenantId, new StockLedgerReportQuery
        {
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId
        });
        Assert.Equal(50m, storageReport.ClosingQuantity);
        Assert.Equal(4000m, storageReport.ClosingValue);

        // Warehouse level closing quantity and value equal the sum of individual locations
        Assert.Equal(allLocationsReport.ClosingQuantity, stagingReport.ClosingQuantity + storageReport.ClosingQuantity);
        Assert.Equal(allLocationsReport.ClosingValue, stagingReport.ClosingValue + storageReport.ClosingValue);
    }
}
