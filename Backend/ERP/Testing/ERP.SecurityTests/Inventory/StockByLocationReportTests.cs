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

public class StockByLocationReportTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockByLocationReportTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task GetStockByLocationReport_StorageClassification_ReturnsOnlyStorageLocations()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // Create warehouse
        var wh = Warehouse.Create(tenantId, $"WH_{suffix}", $"Warehouse_{suffix}", city: "TestCity");
        await db.Warehouses.AddAsync(wh);

        // LocationType: Storage (BIN)
        var binType = LocationType.Create(tenantId, $"BIN_{suffix}", "Bin Type", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddAsync(binType);

        // LocationType: Staging (RECEIVING)
        var stagingType = LocationType.Create(tenantId, "RECEIVING", "Receiving Dock", canStoreInventory: true, canPutAway: false);
        await db.LocationTypes.AddAsync(stagingType);

        // Storage Location
        var storageLoc = WarehouseLocation.Create(
            tenantId,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: $"STR_{suffix}",
            locationName: $"Storage Bin {suffix}",
            locationTypeId: binType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(storageLoc);

        // Staging Location
        var stagingLoc = WarehouseLocation.Create(
            tenantId,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: $"STG_{suffix}",
            locationName: $"Staging Dock {suffix}",
            locationTypeId: stagingType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(stagingLoc);

        // Product
        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var product = Product.Create(
            tenantId,
            $"REP-P-{suffix}",
            $"Report Product {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.StandardCost,
            standardCost: 25.50m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // Inflow to Storage (+10 units)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 25.50m
        });

        // Inflow to Staging (+5 units)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 25.50m
        });

        // 1. Query with LocationClassification = "Storage"
        var storageReport = await inventoryService.GetStockByLocationReportAsync(tenantId, new StockByLocationReportQuery
        {
            WarehouseId = wh.WarehouseId,
            LocationClassification = "Storage"
        });

        Assert.NotNull(storageReport);
        Assert.Single(storageReport.Items);
        var storageRow = storageReport.Items[0];
        Assert.Equal(storageLoc.WarehouseLocationId, storageRow.WarehouseLocationId);
        Assert.Equal(10m, storageRow.QuantityOnHand);
        Assert.Equal(10m, storageRow.QuantityAvailable);
        Assert.Equal(25.50m, storageRow.UnitCost);
        Assert.Equal(255.00m, storageRow.TotalCostValue);
        Assert.Equal(1, storageReport.TotalCount);
        Assert.Equal(10m, storageReport.TotalQuantityOnHand);
        Assert.Equal(255.00m, storageReport.TotalStockValue);

        // 2. Query with LocationClassification = "Staging"
        var stagingReport = await inventoryService.GetStockByLocationReportAsync(tenantId, new StockByLocationReportQuery
        {
            WarehouseId = wh.WarehouseId,
            LocationClassification = "Staging"
        });

        Assert.NotNull(stagingReport);
        Assert.Single(stagingReport.Items);
        var stagingRow = stagingReport.Items[0];
        Assert.Equal(stagingLoc.WarehouseLocationId, stagingRow.WarehouseLocationId);
        Assert.Equal(5m, stagingRow.QuantityOnHand);
        Assert.Equal(5m, stagingRow.QuantityAvailable);
        Assert.Equal(25.50m, stagingRow.UnitCost);
        Assert.Equal(127.50m, stagingRow.TotalCostValue);
        Assert.Equal(1, stagingReport.TotalCount);
        Assert.Equal(5m, stagingReport.TotalQuantityOnHand);
        Assert.Equal(127.50m, stagingReport.TotalStockValue);
    }

    [Fact]
    public async Task GetStockByLocationReport_MultiTenantIsolation_EnforcesTenantScope()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantAlpha = TestConstants.TenantAlphaId;
        var tenantBeta = TestConstants.TenantBetaId;
        var suffix = Guid.NewGuid().ToString("N")[..6];

        // Create warehouse for Tenant Alpha
        var wh = Warehouse.Create(tenantAlpha, $"WHA_{suffix}", $"Alpha Warehouse {suffix}", city: "CityA");
        await db.Warehouses.AddAsync(wh);

        var binType = LocationType.Create(tenantAlpha, $"BINA_{suffix}", "Bin Type Alpha", canStoreInventory: true, canPutAway: true);
        await db.LocationTypes.AddAsync(binType);

        var loc = WarehouseLocation.Create(
            tenantAlpha,
            wh.WarehouseId,
            warehouseZoneId: null,
            locationCode: $"STRA_{suffix}",
            locationName: $"Storage Bin Alpha {suffix}",
            locationTypeId: binType.LocationTypeId);
        await db.WarehouseLocations.AddAsync(loc);

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantAlpha);
        var product = Product.Create(
            tenantAlpha,
            $"PROD-A-{suffix}",
            $"Alpha Product {suffix}",
            category.CategoryId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true,
            valuationMethod: ValuationMethod.StandardCost,
            standardCost: 15.00m);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        // Inflow to Alpha
        await postingService.PostMovementAsync(tenantAlpha, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 15.00m
        });

        // Tenant Beta queries for the same warehouse
        var betaReport = await inventoryService.GetStockByLocationReportAsync(tenantBeta, new StockByLocationReportQuery
        {
            WarehouseId = wh.WarehouseId
        });

        Assert.NotNull(betaReport);
        Assert.Empty(betaReport.Items);
        Assert.Equal(0, betaReport.TotalCount);
        Assert.Equal(0m, betaReport.TotalQuantityOnHand);
        Assert.Equal(0m, betaReport.TotalStockValue);
    }
}
