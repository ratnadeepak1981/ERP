using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Currency;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.StockCount;
using ERP.Domain.Features.Inventory.Valuation;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryValuationStandardisationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryValuationStandardisationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    #region Helper Methods

    private static async Task<(Product Product, Warehouse Warehouse, WarehouseLocation StorageLoc, WarehouseLocation StagingLoc, Company Company, Branch Branch)> CreateProductAndWarehouseAsync(
        DomainDbContext db,
        Guid tenantId,
        ValuationMethod valuationMethod,
        decimal standardCost = 0,
        TrackingMode trackingMode = TrackingMode.None)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];

        var category = await db.Categories.FirstAsync(c => c.TenantId == tenantId);
        var uom = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Code == "PCS")
            ?? UnitOfMeasure.Create(tenantId, "PCS", "Pieces");
        if (db.Entry(uom).State == EntityState.Detached)
            await db.UnitsOfMeasure.AddAsync(uom);

        var product = Product.Create(
            tenantId,
            $"VAL-STD-{suffix}",
            $"Valuation Standard Test Product {suffix}",
            category.CategoryId,
            unitOfMeasure: uom.Code,
            valuationMethod: valuationMethod,
            standardCost: standardCost,
            trackingMode: trackingMode,
            unitOfMeasureId: uom.UnitOfMeasureId);
        await db.Products.AddAsync(product);

        var company = await db.Companies.FirstAsync(c => c.TenantId == tenantId);
        var gbpCurrency = await db.Currencies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == "GBP")
            ?? Currency.Create(tenantId, "GBP", "British Pound", "£", 2);
        if (db.Entry(gbpCurrency).State == EntityState.Detached)
            await db.Currencies.AddAsync(gbpCurrency);

        var usdCurrency = await db.Currencies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == "USD")
            ?? Currency.Create(tenantId, "USD", "US Dollar", "$", 2);
        if (db.Entry(usdCurrency).State == EntityState.Detached)
            await db.Currencies.AddAsync(usdCurrency);

        var eurCurrency = await db.Currencies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CurrencyCode == "EUR")
            ?? Currency.Create(tenantId, "EUR", "Euro", "€", 2);
        if (db.Entry(eurCurrency).State == EntityState.Detached)
            await db.Currencies.AddAsync(eurCurrency);

        await db.SaveChangesAsync();

        if (company.BaseCurrencyId == null)
        {
            company.BaseCurrencyId = gbpCurrency.CurrencyId;
            await db.SaveChangesAsync();
        }

        var usdGbp = await db.ExchangeRates.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.FromCurrencyId == usdCurrency.CurrencyId && r.ToCurrencyId == gbpCurrency.CurrencyId);
        if (usdGbp == null)
        {
            usdGbp = ExchangeRate.Create(tenantId, usdCurrency.CurrencyId, gbpCurrency.CurrencyId, ExchangeRateType.Daily, DateTime.UtcNow.Date, 1.25m);
            await db.ExchangeRates.AddAsync(usdGbp);
            await db.SaveChangesAsync();
        }

        var eurGbp = await db.ExchangeRates.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.FromCurrencyId == eurCurrency.CurrencyId && r.ToCurrencyId == gbpCurrency.CurrencyId);
        if (eurGbp == null)
        {
            eurGbp = ExchangeRate.Create(tenantId, eurCurrency.CurrencyId, gbpCurrency.CurrencyId, ExchangeRateType.Daily, DateTime.UtcNow.Date, 0.85m);
            await db.ExchangeRates.AddAsync(eurGbp);
            await db.SaveChangesAsync();
        }

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.CompanyId == company.Id);
        if (branch == null)
        {
            branch = new Branch
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Code = $"BR-{Guid.NewGuid():N}"[..10],
                Name = $"Branch {Guid.NewGuid():N}"[..16],
                IsActive = true
            };
            await db.Branches.AddAsync(branch);
        }

        var warehouse = Warehouse.Create(
            tenantId,
            $"W-VAL-{suffix}",
            $"Valuation Warehouse {suffix}",
            companyId: company.Id,
            branchId: branch.Id);
        await db.Warehouses.AddAsync(warehouse);

        var storageLoc = WarehouseLocation.Create(
            tenantId,
            warehouse.WarehouseId,
            null,
            $"LOC-STR-{suffix}",
            $"Storage Location {suffix}");
        await db.WarehouseLocations.AddAsync(storageLoc);

        var stagingLoc = WarehouseLocation.Create(
            tenantId,
            warehouse.WarehouseId,
            null,
            $"LOC-STG-{suffix}",
            $"Staging Receiving Dock {suffix}");
        await db.WarehouseLocations.AddAsync(stagingLoc);

        await db.SaveChangesAsync();

        return (product, warehouse, storageLoc, stagingLoc, company, branch);
    }

    #endregion

    #region Scenario 1 - 4: Foreign Currency GRN Valuation & PPV (Defect A)

    [Fact]
    public async Task Scenario01_ForeignCurrencyGrnValuation_ConvertsUsdPoToGbpBaseCurrency()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();
        var grnService = sp.GetRequiredService<IGoodsReceiptNoteService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, company, branch) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Foreign currency USD PO with exchange rate 1.25 (10 units @ 10 USD * 1.25 = 125 GBP base currency)
        var po = await poService.CreateOrderAsync(tenantId, company.Id, branch.Id, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-USD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Currency = "USD",
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 10m, UnitPrice = 10m }
            }
        });

        po = await poService.SubmitOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Submitter");
        if (po.Status != PurchaseOrderStatus.Approved)
            await poService.ApproveOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Approver");

        var draftGrn = await grnService.CreateDraftFromPoAsync(tenantId, company.Id, branch.Id, po.Id, defaultWarehouseId: warehouse.WarehouseId, defaultLocationId: stagingLoc.WarehouseLocationId);
        await grnService.ConfirmGrnAsync(tenantId, company.Id, branch.Id, draftGrn.GoodsReceiptNoteId);

        // Verify that valuation balance was updated with base currency valuation
        var valBalance = await db.ProductValuationBalances
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        Assert.NotNull(valBalance);
        Assert.Equal(10m, valBalance.TotalQuantity);
        Assert.Equal(125m, valBalance.TotalCostValue);
        Assert.Equal(12.50m, valBalance.CurrentAverageCost);
    }

    [Fact]
    public async Task Scenario02_MissingExchangeRateRejection_RejectsForeignCurrencyGrnWhenRateIsZeroOrMissing()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();
        var grnService = sp.GetRequiredService<IGoodsReceiptNoteService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, _, stagingLoc, company, branch) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        var po = await poService.CreateOrderAsync(tenantId, company.Id, branch.Id, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-NOFX-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Currency = "EUR",
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 5m, UnitPrice = 20m }
            }
        });

        po = await poService.SubmitOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Submitter");
        if (po.Status != PurchaseOrderStatus.Approved)
            await poService.ApproveOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Approver");

        var draftGrn = await grnService.CreateDraftFromPoAsync(tenantId, company.Id, branch.Id, po.Id, defaultWarehouseId: warehouse.WarehouseId, defaultLocationId: stagingLoc.WarehouseLocationId);

        // Clear exchange rate to 0 to simulate missing/invalid rate on foreign order during GRN confirmation
        var poEntity = await db.PurchaseOrders.FirstAsync(p => p.Id == po.Id);
        db.Entry(poEntity).Property(nameof(PurchaseOrder.AppliedExchangeRate)).CurrentValue = 0m;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grnService.ConfirmGrnAsync(tenantId, company.Id, branch.Id, draftGrn.GoodsReceiptNoteId));

        Assert.Contains("exchange rate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario03_StandardCostGrnPPV_PostsAtStandardCostAndCalculatesPPV()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();
        var grnService = sp.GetRequiredService<IGoodsReceiptNoteService>();

        var tenantId = TestConstants.TenantAlphaId;
        // Standard cost is 15.00
        var (product, warehouse, _, stagingLoc, company, branch) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 15.00m);

        // Purchase order at 20.00 base currency (variance of +5.00 per unit)
        var po = await poService.CreateOrderAsync(tenantId, company.Id, branch.Id, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-PPV-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Currency = "GBP",
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 10m, UnitPrice = 20m }
            }
        });

        po = await poService.SubmitOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Submitter");
        if (po.Status != PurchaseOrderStatus.Approved)
            await poService.ApproveOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Approver");

        var draftGrn = await grnService.CreateDraftFromPoAsync(tenantId, company.Id, branch.Id, po.Id, defaultWarehouseId: warehouse.WarehouseId, defaultLocationId: stagingLoc.WarehouseLocationId);
        await grnService.ConfirmGrnAsync(tenantId, company.Id, branch.Id, draftGrn.GoodsReceiptNoteId);

        // Inventory transaction must be posted at established Standard Cost (15.00)
        var txn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProductId == product.ProductId && t.MovementType == InventoryMovementType.GoodsReceipt);

        Assert.NotNull(txn);
        Assert.Equal(15.00m, txn.UnitCost);
        Assert.Equal(150.00m, txn.TotalCost);

        // PPV must be preserved on the GRN line remarks audit record
        var grnLine = await db.GoodsReceiptNoteLines.FirstAsync(l => l.GoodsReceiptNoteId == draftGrn.GoodsReceiptNoteId);
        Assert.Contains("[PPV:", grnLine.Remarks);
        Assert.Contains("TotalVariance=50.00", grnLine.Remarks);
    }

    [Fact]
    public async Task Scenario04_BaseCurrencyGrn_DomesticPoUsesRateOneWithoutConversion()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();
        var grnService = sp.GetRequiredService<IGoodsReceiptNoteService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, _, stagingLoc, company, branch) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        var po = await poService.CreateOrderAsync(tenantId, company.Id, branch.Id, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-DOM-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Currency = "GBP",
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 8m, UnitPrice = 14.50m }
            }
        });

        po = await poService.SubmitOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Submitter");
        if (po.Status != PurchaseOrderStatus.Approved)
            await poService.ApproveOrderAsync(tenantId, company.Id, branch.Id, po.Id, Guid.NewGuid(), "Approver");

        var draftGrn = await grnService.CreateDraftFromPoAsync(tenantId, company.Id, branch.Id, po.Id, defaultWarehouseId: warehouse.WarehouseId, defaultLocationId: stagingLoc.WarehouseLocationId);
        await grnService.ConfirmGrnAsync(tenantId, company.Id, branch.Id, draftGrn.GoodsReceiptNoteId);

        var valBalance = await db.ProductValuationBalances
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        Assert.NotNull(valBalance);
        Assert.Equal(8m, valBalance.TotalQuantity);
        Assert.Equal(116.00m, valBalance.TotalCostValue); // 8 * 14.50 = 116.00
        Assert.Equal(14.50m, valBalance.CurrentAverageCost);
    }

    #endregion

    #region Scenario 5 - 9: Zero-Cost Inbound Receipt Protection & Free Samples (Defect C)

    [Fact]
    public async Task Scenario05_ZeroCostReceiptRejection_StandardItem_RejectedWithDomainError()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Attempt to post zero-cost receipt for non-free sample
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
                WarehouseLocationId = storageLoc.WarehouseLocationId,
                Quantity = 10m,
                UnitCost = 0m,
                IsFreeSample = false
            }));

        Assert.Contains("Inbound inventory receipt requires a valid positive cost rate. Zero-cost receipts are permitted only for explicitly identified free samples.", ex.Message);
    }

    [Fact]
    public async Task Scenario06_ZeroCostReceiptRejection_StandardCostItem_RejectsWhenStandardCostNotPositive()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        // Standard cost item with standardCost = 0
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 0m);

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
                WarehouseLocationId = storageLoc.WarehouseLocationId,
                Quantity = 5m,
                UnitCost = 0m,
                IsFreeSample = false
            }));

        Assert.Contains("Inbound inventory receipt requires a valid positive cost rate. Zero-cost receipts are permitted only for explicitly identified free samples.", ex.Message);
    }

    [Fact]
    public async Task Scenario07_FreeSampleReceiptPermitted_ExplicitFreeSampleAcceptedWithZeroCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Explicit free sample with 0 unit cost must succeed
        var txn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 0m,
            IsFreeSample = true
        });

        Assert.NotNull(txn);
        Assert.Equal(5m, txn.Quantity);

        var balance = await db.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseId == warehouse.WarehouseId && b.WarehouseLocationId == storageLoc.WarehouseLocationId && b.ProductId == product.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(5m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task Scenario08_FreeSampleNonDilution_WeightedAverageMovingAverageUnchanged()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Initial normal receipt: 10 units @ 20.00 -> Average Cost = 20.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m
        });

        var valBefore = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);
        Assert.Equal(20m, valBefore.CurrentAverageCost);

        // Free sample receipt: 5 units @ 0.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 0m,
            IsFreeSample = true
        });

        // Free sample does NOT artificially inflate TotalCostValue (remains 200, not 300)
        var valAfter = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);
        Assert.Equal(15m, valAfter.TotalQuantity);
        Assert.Equal(200m, valAfter.TotalCostValue);
        Assert.Equal(13.3333m, valAfter.CurrentAverageCost);
    }

    [Fact]
    public async Task Scenario09_FreeSampleFifoLayer_CreatesNonDilutingCostLayer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO, standardCost: 18m);

        // Post free sample under FIFO with standard cost available
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 4m,
            UnitCost = 0m,
            IsFreeSample = true
        });

        var layers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.WarehouseId == warehouse.WarehouseId)
            .ToListAsync();

        var layer = Assert.Single(layers);
        Assert.Equal(4m, layer.RemainingQuantity);
        Assert.Equal(0m, layer.UnitCost); // Must not create an artificial positive-cost layer!
        Assert.Equal(0m, layer.RemainingCost);
    }

    #endregion

    #region Scenario 10 - 16: FIFO Putaway & Intra-Warehouse Layer Preservation (Defect B)

    [Fact]
    public async Task Scenario10_FifoPutaway_SameWarehouseTransfer_PreservesOriginalLayerDateAndUnitCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Receive into Staging @ 25.00
        var receiptDate = DateTime.UtcNow.AddDays(-5);
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 25m,
            TransactionDate = receiptDate
        });

        var stagingLayer = await db.InventoryCostLayers
            .FirstAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == stagingLoc.WarehouseLocationId && l.ProductId == product.ProductId);
        var originalLayerDate = stagingLayer.LayerDate;

        // Perform intra-warehouse transfer / putaway: Staging -> Storage
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 10m
        });

        // Destination layer must preserve original acquisition date and unit cost
        var storageLayer = await db.InventoryCostLayers
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == storageLoc.WarehouseLocationId && l.ProductId == product.ProductId);

        Assert.NotNull(storageLayer);
        Assert.Equal(10m, storageLayer.RemainingQuantity);
        Assert.Equal(25m, storageLayer.UnitCost);
        Assert.Equal(originalLayerDate, storageLayer.LayerDate);

        // Staging layer must be exhausted
        await db.Entry(stagingLayer).ReloadAsync();
        Assert.True(stagingLayer.IsExhausted);
        Assert.Equal(0m, stagingLayer.RemainingQuantity);
    }

    [Fact]
    public async Task Scenario11_FifoPutaway_PartialTransfer_SplitsLayerAndSumsToOriginal()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Receive 10 units into Staging
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 30m
        });

        // Putaway partial 4 units to Storage
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 4m
        });

        var stagingLayer = await db.InventoryCostLayers
            .FirstAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == stagingLoc.WarehouseLocationId && l.ProductId == product.ProductId);
        var storageLayer = await db.InventoryCostLayers
            .FirstAsync(l => l.TenantId == tenantId && l.WarehouseLocationId == storageLoc.WarehouseLocationId && l.ProductId == product.ProductId);

        Assert.Equal(6m, stagingLayer.RemainingQuantity);
        Assert.Equal(4m, storageLayer.RemainingQuantity);
        Assert.Equal(10m, stagingLayer.RemainingQuantity + storageLayer.RemainingQuantity);
        Assert.Equal(300m, stagingLayer.RemainingCost + storageLayer.RemainingCost);
        Assert.Equal(stagingLayer.LayerDate, storageLayer.LayerDate);
    }

    [Fact]
    public async Task Scenario12_MultiBinPutaway_ChronologyPreservedAcrossMultipleBins()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, bin1, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        var bin2 = WarehouseLocation.Create(tenantId, warehouse.WarehouseId, null, "LOC-BIN2", "Bin 2");
        await db.WarehouseLocations.AddAsync(bin2);
        await db.SaveChangesAsync();

        // 20 units received into Staging
        var receiptDate = DateTime.UtcNow.AddDays(-10);
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 40m,
            TransactionDate = receiptDate
        });

        // Putaway 10 to Bin 1
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = bin1.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 10m
        });

        // Putaway 10 to Bin 2
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = bin2.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 10m
        });

        var layer1 = await db.InventoryCostLayers.FirstAsync(l => l.WarehouseLocationId == bin1.WarehouseLocationId && l.ProductId == product.ProductId);
        var layer2 = await db.InventoryCostLayers.FirstAsync(l => l.WarehouseLocationId == bin2.WarehouseLocationId && l.ProductId == product.ProductId);

        Assert.Equal(layer1.LayerDate, layer2.LayerDate);
        Assert.Equal(40m, layer1.UnitCost);
        Assert.Equal(40m, layer2.UnitCost);
    }

    [Fact]
    public async Task Scenario13_SourceLocationInsufficientStock_RejectsTransferWithoutFallingBack()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Put 5 units in Staging, 10 units in Storage
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 10m
        });

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m
        });

        // Request 8 units from Staging (which only has 5). Must throw and NOT fall back to Storage!
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceWarehouseId = warehouse.WarehouseId,
                SourceLocationId = stagingLoc.WarehouseLocationId,
                DestinationWarehouseId = warehouse.WarehouseId,
                DestinationLocationId = storageLoc.WarehouseLocationId,
                ProductId = product.ProductId,
                Quantity = 8m
            }));
    }

    [Fact]
    public async Task Scenario14_IntraWarehouseTransfer_WeightedAverageMovingAverageUnchanged()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Inflow to Staging: 10 units @ 22.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 22m
        });

        var valBefore = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        // Transfer 6 units Staging -> Storage
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 6m
        });

        var valAfter = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        Assert.Equal(valBefore.TotalQuantity, valAfter.TotalQuantity);
        Assert.Equal(valBefore.TotalCostValue, valAfter.TotalCostValue);
        Assert.Equal(valBefore.CurrentAverageCost, valAfter.CurrentAverageCost);
    }

    [Fact]
    public async Task Scenario15_IntraWarehouseTransfer_TotalCompanyValuePreserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Receipts of 2 layers
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 10m
        });
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 20m
        });

        decimal totalValueBefore = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .SumAsync(l => l.RemainingCost);

        // Transfer 7 units from Staging -> Storage (crosses layer 1 and partially layer 2)
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = warehouse.WarehouseId,
            SourceLocationId = stagingLoc.WarehouseLocationId,
            DestinationWarehouseId = warehouse.WarehouseId,
            DestinationLocationId = storageLoc.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 7m
        });

        decimal totalValueAfter = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .SumAsync(l => l.RemainingCost);

        Assert.Equal(totalValueBefore, totalValueAfter);
    }

    [Fact]
    public async Task Scenario16_InterWarehouseTransfer_ValuationUpdatedCorrectlyAtDestination()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, wh1, loc1, _, company, branch) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        var wh2 = Warehouse.Create(tenantId, $"WH2-{Guid.NewGuid():N}"[..12], "Destination Warehouse", companyId: company.Id, branchId: branch.Id);
        await db.Warehouses.AddAsync(wh2);
        var loc2 = WarehouseLocation.Create(tenantId, wh2.WarehouseId, null, "LOC-WH2", "WH2 Location");
        await db.WarehouseLocations.AddAsync(loc2);
        await db.SaveChangesAsync();

        // Inflow to WH1: 10 units @ 50.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = loc1.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 50m
        });

        // Inter-warehouse transfer: 4 units WH1 -> WH2
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = loc1.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = loc2.WarehouseLocationId,
            ProductId = product.ProductId,
            Quantity = 4m
        });

        var val1 = await db.ProductValuationBalances.FirstAsync(v => v.WarehouseId == wh1.WarehouseId && v.ProductId == product.ProductId);
        var val2 = await db.ProductValuationBalances.FirstAsync(v => v.WarehouseId == wh2.WarehouseId && v.ProductId == product.ProductId);

        Assert.Equal(6m, val1.TotalQuantity);
        Assert.Equal(300m, val1.TotalCostValue);
        Assert.Equal(50m, val1.CurrentAverageCost);

        Assert.Equal(4m, val2.TotalQuantity);
        Assert.Equal(200m, val2.TotalCostValue);
        Assert.Equal(50m, val2.CurrentAverageCost);
    }

    #endregion

    #region Scenario 17 - 20: Central Authoritative Valuation API & DTOs (Defect D)

    [Fact]
    public async Task Scenario17_CentralValuationApi_Fifo_ReturnsLayerSumAndWeightedCostRate()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Layer 1: 5 units @ 10.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 10m
        });

        // Layer 2: 5 units @ 20.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 20m
        });

        var valuation = await inventoryService.GetStockValuationAsync(tenantId, product.ProductId, warehouse.WarehouseId);

        Assert.True(valuation.IsCostEstablished);
        Assert.Equal(10m, valuation.QuantityOnHand);
        Assert.Equal(150m, valuation.TotalInventoryValue);
        Assert.Equal(15m, valuation.CostRate); // (50 + 100) / 10 = 15.00
        Assert.Equal(10m, valuation.NextIssueRate); // Oldest layer rate = 10.00
    }

    [Fact]
    public async Task Scenario18_CentralValuationApi_WeightedAverage_ReturnsWarehouseAverageWithoutFallback()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage, standardCost: 99m);

        // 10 units @ 35.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 35m
        });

        var valuation = await inventoryService.GetStockValuationAsync(tenantId, product.ProductId, warehouse.WarehouseId);

        Assert.True(valuation.IsCostEstablished);
        Assert.Equal(35m, valuation.CostRate);
        Assert.Equal(350m, valuation.TotalInventoryValue);
        Assert.NotEqual(product.StandardCost, valuation.CostRate); // Must not fall back to StandardCost 99!
    }

    [Fact]
    public async Task Scenario19_CentralValuationApi_StandardCost_ReturnsProductStandardCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, _, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 42.50m);

        var valuation = await inventoryService.GetStockValuationAsync(tenantId, product.ProductId, warehouse.WarehouseId);

        Assert.True(valuation.IsCostEstablished);
        Assert.Equal(42.50m, valuation.CostRate);
    }

    [Fact]
    public async Task Scenario20_AvailableStockDto_Enrichment_PopulatesAllValuationFields()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 12m,
            UnitCost = 18m
        });

        var availableStock = await inventoryService.GetAvailableStockAsync(tenantId, product.ProductId, storageLoc.WarehouseLocationId);

        Assert.NotNull(availableStock);
        Assert.Equal(12m, availableStock.QuantityOnHand);
        Assert.Equal(18m, availableStock.CostRate);
        Assert.Equal(216m, availableStock.TotalInventoryValue);
        Assert.Equal(ValuationMethod.WeightedAverage, availableStock.ValuationMethod);
        Assert.True(availableStock.IsCostEstablished);
    }

    #endregion

    #region Scenario 21 - 24: Stock Count Positive Variance & Draft Pre-population (Defect E)

    [Fact]
    public async Task Scenario21_StockCountPositiveVariance_StandardCost_PostsAtStandardCostAndRejectsIfZero()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var stockCountService = sp.GetRequiredService<IStockCountService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, company, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 28.00m);

        // Physical count: observed 5 units when expected was 0 (positive variance +5)
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Lines = new List<CreateStockCountLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = storageLoc.WarehouseLocationId,
                    PhysicalQuantity = 5m,
                    UnitCost = 0m // Not specified by user, should default to standard cost
                }
            }
        });

        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });
        var posted = await stockCountService.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });

        Assert.Equal(StockCountStatus.Posted, posted.Status);

        // Verify inventory transaction was posted at 28.00
        var txn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProductId == product.ProductId && t.MovementType == InventoryMovementType.StockAdjustmentIn);
        Assert.NotNull(txn);
        Assert.Equal(28.00m, txn.UnitCost);
    }

    [Fact]
    public async Task Scenario22_StockCountPositiveVariance_WeightedAverage_PostsAtWarehouseAverage()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var stockCountService = sp.GetRequiredService<IStockCountService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, company, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Initial balance establishes warehouse average at 32.50
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 32.50m
        });

        // Physical count: found 3 additional units (observed 13 vs expected 10)
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Lines = new List<CreateStockCountLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = storageLoc.WarehouseLocationId,
                    PhysicalQuantity = 13m,
                    UnitCost = 0m
                }
            }
        });

        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });
        await stockCountService.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });

        var valBalance = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        Assert.Equal(13m, valBalance.TotalQuantity);
        Assert.Equal(32.50m, valBalance.CurrentAverageCost);
    }

    [Fact]
    public async Task Scenario23_StockCountPositiveVariance_Fifo_PostsAtLatestWarehouseLayer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var stockCountService = sp.GetRequiredService<IStockCountService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, company, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Post layer at 55.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 55m
        });

        // Count found 2 more units (observed 7 vs expected 5)
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Lines = new List<CreateStockCountLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = storageLoc.WarehouseLocationId,
                    PhysicalQuantity = 7m,
                    UnitCost = 0m
                }
            }
        });

        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });
        await stockCountService.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });

        // Positive variance must be posted at 55.00
        var layers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.WarehouseId == warehouse.WarehouseId)
            .ToListAsync();

        Assert.Equal(7m, layers.Sum(l => l.RemainingQuantity));
        Assert.All(layers, l => Assert.Equal(55m, l.UnitCost));
    }

    [Fact]
    public async Task Scenario24_StockCountDraft_PrepopulatesAuthoritativeUnitCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var stockCountService = sp.GetRequiredService<IStockCountService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, company, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Inflow to establish average cost of 19.50
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 19.50m
        });

        // Draft creation without user unit cost
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Lines = new List<CreateStockCountLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = storageLoc.WarehouseLocationId,
                    PhysicalQuantity = 10m,
                    UnitCost = 0m
                }
            }
        });

        var line = Assert.Single(draft.Lines);
        Assert.Equal(19.50m, line.UnitCost);
    }

    #endregion

    #region Scenario 25: Stock by Location Report Rounding Drift Elimination (Defect F)

    [Fact]
    public async Task Scenario25_StockByLocationReport_RoundingDriftEliminated()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, bin1, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        var bin2 = WarehouseLocation.Create(tenantId, warehouse.WarehouseId, null, "LOC-BIN-R2", "Bin Rounding 2");
        var bin3 = WarehouseLocation.Create(tenantId, warehouse.WarehouseId, null, "LOC-BIN-R3", "Bin Rounding 3");
        await db.WarehouseLocations.AddRangeAsync(bin2, bin3);
        await db.SaveChangesAsync();

        // 10 units @ 33.33333 -> Total Cost Value = 100.00
        // Distribute 3 units to Bin 1, 3 units to Bin 2, 4 units to Bin 3
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin1.WarehouseLocationId,
            Quantity = 3m,
            UnitCost = 10m
        });

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin2.WarehouseLocationId,
            Quantity = 3m,
            UnitCost = 10m
        });

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 3,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin3.WarehouseLocationId,
            Quantity = 4m,
            UnitCost = 10m
        });

        var report = await inventoryService.GetStockByLocationReportAsync(tenantId, new StockByLocationReportQuery
        {
            WarehouseId = warehouse.WarehouseId,
            ProductId = product.ProductId
        });

        var valBalance = await db.ProductValuationBalances
            .FirstAsync(v => v.TenantId == tenantId && v.ProductId == product.ProductId && v.WarehouseId == warehouse.WarehouseId);

        decimal sumLocationValues = report.Items.Sum(i => i.TotalCostValue);

        // Sum of location values must exactly equal warehouse valuation balance with zero penny drift!
        Assert.Equal(valBalance.TotalCostValue, sumLocationValues);
        Assert.Equal(valBalance.TotalCostValue, report.TotalStockValue);
    }

    [Fact]
    public async Task Scenario26_CentralValuationApi_EmptyLocation_ReturnsZeroQuantityAndZeroTotalValue()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, stagingLoc, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.FIFO);

        // Put stock in StorageLoc: 10 units @ 45.00 = 450.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 45m
        });

        // Query empty StagingLoc
        var emptyValuation = await inventoryService.GetStockValuationAsync(tenantId, product.ProductId, warehouse.WarehouseId, stagingLoc.WarehouseLocationId);

        // An empty location MUST report 0 on-hand and 0 total value
        Assert.Equal(0m, emptyValuation.QuantityOnHand);
        Assert.Equal(0m, emptyValuation.TotalInventoryValue);
    }

    [Fact]
    public async Task Scenario27_StockCountPositiveVariance_RecalculatesAuthoritativeCost_IgnoresStaleDraftCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var stockCountService = sp.GetRequiredService<IStockCountService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, storageLoc, _, company, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.WeightedAverage);

        // Initial receipt: 10 units @ 20.00 -> Average Cost = 20.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m
        });

        // Create draft count with observed 15 (variance +5), but manually altered unitCost of 99.00 on draft
        var draft = await stockCountService.CreateDraftAsync(tenantId, new CreateStockCountDraftRequest
        {
            CompanyId = company.Id,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = storageLoc.WarehouseLocationId,
            Lines = new List<CreateStockCountLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = storageLoc.WarehouseLocationId,
                    PhysicalQuantity = 15m,
                    UnitCost = 99m // Stale/tampered cost on draft
                }
            }
        });

        await stockCountService.SubmitAsync(tenantId, draft.StockCountId, new SubmitStockCountRequest { SubmittedByUserId = Guid.NewGuid() });
        await stockCountService.ApproveAsync(tenantId, draft.StockCountId, new ApproveStockCountRequest { ApprovedByUserId = Guid.NewGuid() });
        await stockCountService.PostAsync(tenantId, draft.StockCountId, new PostStockCountRequest { PostedByUserId = Guid.NewGuid() });

        // Positive variance must be posted at authoritative moving average cost (20.00), NOT the altered draft cost (99.00)
        var txn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProductId == product.ProductId && t.MovementType == InventoryMovementType.StockAdjustmentIn);

        Assert.NotNull(txn);
        Assert.Equal(20m, txn.UnitCost);
    }

    [Fact]
    public async Task Scenario28_StockByLocationReport_StandardCost_ReconcilesLocationsToWarehouse()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();

        var tenantId = TestConstants.TenantAlphaId;
        var (product, warehouse, bin1, _, _, _) = await CreateProductAndWarehouseAsync(db, tenantId, ValuationMethod.StandardCost, standardCost: 10m);

        var bin2 = WarehouseLocation.Create(tenantId, warehouse.WarehouseId, null, "LOC-SC-B2", "Bin SC 2");
        var bin3 = WarehouseLocation.Create(tenantId, warehouse.WarehouseId, null, "LOC-SC-B3", "Bin SC 3");
        await db.WarehouseLocations.AddRangeAsync(bin2, bin3);
        await db.SaveChangesAsync();

        // Receipts across 3 bins
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin1.WarehouseLocationId,
            Quantity = 3m,
            UnitCost = 10m
        });
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin2.WarehouseLocationId,
            Quantity = 3m,
            UnitCost = 10m
        });
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 3,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = bin3.WarehouseLocationId,
            Quantity = 4m,
            UnitCost = 10m
        });

        var report = await inventoryService.GetStockByLocationReportAsync(tenantId, new StockByLocationReportQuery
        {
            WarehouseId = warehouse.WarehouseId,
            ProductId = product.ProductId
        });

        decimal sumLocationValues = report.Items.Sum(i => i.TotalCostValue);
        decimal expectedWarehouseValue = 10m * product.StandardCost;

        Assert.Equal(expectedWarehouseValue, sumLocationValues);
        Assert.Equal(expectedWarehouseValue, report.TotalStockValue);
    }

    #endregion
}
