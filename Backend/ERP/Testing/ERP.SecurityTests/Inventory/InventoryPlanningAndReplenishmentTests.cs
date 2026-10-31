using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.Inventory.Planning;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class InventoryPlanningAndReplenishmentTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryPlanningAndReplenishmentTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Planning_ProductDefaults_TriggersReorderWhenAtOrBelowReorderLevel()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        // Product default: ReorderLevel = 50, ReorderQuantity = 100
        product.UpdatePlanningSettings(reorderLevel: 50m, reorderQuantity: 100m);
        await db.SaveChangesAsync();

        // 1. Initially On-Hand = 0 <= 50 -> Reorder triggered
        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.True(s.IsReorderTriggered);
        Assert.Equal(ReplenishmentUrgency.CriticalStockoutRisk, s.Urgency); // Net <= 0
        Assert.Equal(100m, s.SuggestedReplenishmentQuantity);
        Assert.Equal(50m, s.EffectiveReorderLevel);
        Assert.Equal(100m, s.EffectiveReorderQuantity);
        Assert.False(s.IsWarehouseOverrideApplied);
    }

    [Fact]
    public async Task Planning_WarehouseOverride_TakesPrecedenceOverProductDefaults()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        // Product default: ReorderLevel = 20, ReorderQuantity = 50
        product.UpdatePlanningSettings(reorderLevel: 20m, reorderQuantity: 50m);

        // Warehouse override: ReorderLevel = 80, ReorderQuantity = 200, MaxStock = 300
        var whSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 80m,
            reorderQuantity: 200m,
            maxStockQuantity: 300m);
        db.ProductWarehouseSettings.Add(whSetting);
        await db.SaveChangesAsync();

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.True(s.IsWarehouseOverrideApplied);
        Assert.Equal(80m, s.EffectiveReorderLevel);
        Assert.Equal(200m, s.EffectiveReorderQuantity);
        Assert.Equal(300m, s.EffectiveMaxStockQuantity);
        // Sizing: deficit = 300 - 0 = 300, max(300, 200) = 300
        Assert.Equal(300m, s.SuggestedReplenishmentQuantity);
    }

    [Fact]
    public async Task Planning_ExplicitZeroReorderLevelOverride_PreservedAndDoesNotTriggerIfPositiveStock()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        // Product default: ReorderLevel = 50
        product.UpdatePlanningSettings(reorderLevel: 50m, reorderQuantity: 100m);

        // Warehouse override: ReorderLevel = 0m (explicit zero override)
        var whSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 0m,
            reorderQuantity: 50m);
        db.ProductWarehouseSettings.Add(whSetting);
        await db.SaveChangesAsync();

        // Inflow 10 units on-hand
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m
        });

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.True(s.IsWarehouseOverrideApplied);
        Assert.Equal(0m, s.EffectiveReorderLevel);
        Assert.Equal(10m, s.NetAvailablePlanningStock);
        // With explicit zero reorder level and positive stock (10 > 0), reorder is NOT triggered
        Assert.False(s.IsReorderTriggered);
        Assert.Equal(ReplenishmentUrgency.Normal, s.Urgency);
    }

    [Fact]
    public async Task Planning_InactiveWarehouseOverride_FallsBackToProductDefaults()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        product.UpdatePlanningSettings(reorderLevel: 30m, reorderQuantity: 60m);

        var whSetting = ProductWarehouseSetting.Create(
            tenantId,
            product.ProductId,
            warehouse.WarehouseId,
            reorderLevel: 100m,
            reorderQuantity: 200m);
        whSetting.Deactivate();
        db.ProductWarehouseSettings.Add(whSetting);
        await db.SaveChangesAsync();

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.False(s.IsWarehouseOverrideApplied);
        Assert.Equal(30m, s.EffectiveReorderLevel);
        Assert.Equal(60m, s.EffectiveReorderQuantity);
    }

    [Fact]
    public async Task Planning_BelowSafetyStock_MarksCriticalStockoutRisk()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        product.UpdatePlanningSettings(
            reorderLevel: 40m,
            reorderQuantity: 80m,
            minStockQuantity: 20m,
            safetyStockQuantity: 10m);
        await db.SaveChangesAsync();

        // Inflow 8 units (8 <= SafetyStock of 10)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 8m,
            UnitCost = 15m
        });

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.Equal(8m, s.NetAvailablePlanningStock);
        Assert.True(s.IsBelowSafetyStock);
        Assert.True(s.IsBelowMinimumStock);
        Assert.True(s.IsReorderTriggered);
        Assert.Equal(ReplenishmentUrgency.CriticalStockoutRisk, s.Urgency);
    }

    [Fact]
    public async Task Planning_ExpiredBatchesAndQuarantine_ExcludedFromPlanningAvailability()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.Batch);

        product.UpdatePlanningSettings(reorderLevel: 50m, reorderQuantity: 100m);
        await db.SaveChangesAsync();

        var asOfDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Batch 1: Expired (Expiry: 2026-05-01) - 40 units
        string batchExpired = $"EXP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 40m,
            UnitCost = 10m,
            BatchNumber = batchExpired,
            ExpiryDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Batch 2: Valid Active (Expiry: 2027-01-01) - 30 units
        string batchValid = $"VAL-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 30m,
            UnitCost = 10m,
            BatchNumber = batchValid,
            ExpiryDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Batch 3: Quarantine - 15 units (Valid expiry, but quarantined)
        string batchQuarantine = $"QC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 15m,
            UnitCost = 10m,
            BatchNumber = batchQuarantine,
            ExpiryDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsQuarantine = true
        });

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId, asOfDate: asOfDate);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        // Total physical on-hand = 40 + 30 + 15 = 85
        Assert.Equal(85m, s.QuantityOnHand);
        // Expired = 40
        Assert.Equal(40m, s.ExpiredQuantity);
        // Eligible on-hand = 85 - 40 = 45
        Assert.Equal(45m, s.EligibleOnHandQuantity);
        // Quarantine = 15
        Assert.Equal(15m, s.QuantityQuarantine);
        // Net Available = 45 - 15 = 30
        Assert.Equal(30m, s.NetAvailablePlanningStock);
        // Since Net Available 30 <= ReorderLevel 50, reorder is triggered
        Assert.True(s.IsReorderTriggered);
    }

    [Fact]
    public async Task Planning_AllocationsAndNegativeAvailability_MarksCritical()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        product.UpdatePlanningSettings(reorderLevel: 25m, reorderQuantity: 50m);
        await db.SaveChangesAsync();

        // Inflow 20 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 10m
        });

        // Allocate 18 units to pending demand
        var balance = await db.InventoryBalances.FirstAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId);
        balance.ApplyAllocationChange(18m);
        await db.SaveChangesAsync();

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.Equal(20m, s.QuantityOnHand);
        Assert.Equal(18m, s.QuantityAllocated);
        Assert.Equal(2m, s.NetAvailablePlanningStock); // 20 - 18 = 2
        Assert.True(s.IsReorderTriggered); // 2 <= 25
    }

    [Fact]
    public async Task Planning_MissingQuantityConfiguration_ReturnsConfigurationWarning()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = Guid.NewGuid();
        var (warehouse, loc, category, product) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);

        // Product with NO reorder qty and NO max stock
        product.UpdatePlanningSettings(reorderLevel: 10m, reorderQuantity: 0m, maxStockQuantity: null);
        await db.SaveChangesAsync();

        var suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, warehouse.WarehouseId);
        var s = Assert.Single(suggestions, x => x.ProductId == product.ProductId);

        Assert.True(s.IsReorderTriggered);
        Assert.Equal(0m, s.SuggestedReplenishmentQuantity);
        Assert.NotNull(s.ConfigurationWarning);
        Assert.Contains("No valid replenishment quantity could be determined", s.ConfigurationWarning);
    }

    [Fact]
    public async Task Planning_CategoryAndWarehouseFiltering_FiltersAppropriately()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = Guid.NewGuid();
        var (wh1, loc1, cat1, prod1) = await SetupWarehouseAndProductAsync(db, tenantId, TrackingMode.None);
        var cat2 = Category.Create(tenantId, $"CAT2-{Guid.NewGuid():N}"[..12], "Cat 2");
        db.Categories.Add(cat2);

        var prod2 = Product.Create(tenantId, $"P2-{Guid.NewGuid():N}"[..12], "Prod 2", cat2.CategoryId, "PCS", reorderLevel: 10m, reorderQuantity: 20m);
        db.Products.Add(prod2);
        await db.SaveChangesAsync();

        // Filter by Category 1
        var cat1Suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, wh1.WarehouseId, categoryId: cat1.CategoryId);
        Assert.Single(cat1Suggestions);
        Assert.Equal(prod1.ProductId, cat1Suggestions[0].ProductId);

        // Filter by Category 2
        var cat2Suggestions = await planningService.GetReplenishmentSuggestionsAsync(tenantId, wh1.WarehouseId, categoryId: cat2.CategoryId);
        Assert.Single(cat2Suggestions);
        Assert.Equal(prod2.ProductId, cat2Suggestions[0].ProductId);
    }

    [Fact]
    public async Task PoAndGrnLifecycle_ApprovalIncrementsOnOrder_AndGrnReceiptDecrementsIt()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();
        var grnService = sp.GetRequiredService<IGoodsReceiptNoteService>();
        var planningService = sp.GetRequiredService<IInventoryPlanningService>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;

        var branch = new global::Domain.Features.MasterData.Branch.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = $"BR-{Guid.NewGuid():N}"[..10],
            Name = $"Branch {Guid.NewGuid():N}"[..16],
            IsActive = true
        };
        db.Branches.Add(branch);
        var branchId = branch.Id;

        // Create warehouse associated with this branch
        var wh = Warehouse.Create(tenantId, $"WH-{Guid.NewGuid():N}"[..12], "Branch Main WH", companyId: companyId, branchId: branchId);
        db.Warehouses.Add(wh);

        var loc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, "LOC-REC", "Receiving Dock");
        db.WarehouseLocations.Add(loc);

        var cat = Category.Create(tenantId, $"CAT-{Guid.NewGuid():N}"[..12], $"PO Cat {Guid.NewGuid():N}"[..18]);
        db.Categories.Add(cat);

        var product = Product.Create(tenantId, $"P-PO-{Guid.NewGuid():N}"[..12], $"PO Prod {Guid.NewGuid():N}"[..18], cat.CategoryId, "PCS", reorderLevel: 50m, reorderQuantity: 100m);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // 1. Create Purchase Order for 80 units
        var po = await poService.CreateOrderAsync(tenantId, companyId, branchId, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 80m, UnitPrice = 25m }
            }
        });

        // 2. Submit & Approve PO -> Increments QuantityOnOrder by 80
        po = await poService.SubmitOrderAsync(tenantId, companyId, branchId, po.Id, Guid.NewGuid(), "SubmitterUser");
        if (po.Status != PurchaseOrderStatus.Approved)
        {
            await poService.ApproveOrderAsync(tenantId, companyId, branchId, po.Id, Guid.NewGuid(), "ApproverUser");
        }

        // Verify balance has QuantityOnOrder = 80
        var balAfterPo = await db.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseId == wh.WarehouseId && b.ProductId == product.ProductId);
        Assert.NotNull(balAfterPo);
        Assert.Equal(80m, balAfterPo.QuantityOnOrder);

        // Planning suggestion incorporates QuantityOnOrder
        var suggestionsAfterPo = await planningService.GetReplenishmentSuggestionsAsync(tenantId, wh.WarehouseId);
        var sAfterPo = Assert.Single(suggestionsAfterPo, x => x.ProductId == product.ProductId);
        Assert.Equal(80m, sAfterPo.QuantityOnOrder);
        Assert.Equal(80m, sAfterPo.NetAvailablePlanningStock); // 0 + 80 = 80 > ReorderLevel 50 -> Not triggered!
        Assert.False(sAfterPo.IsReorderTriggered);

        // 3. Create GRN and receive partial 50 units
        var draftGrn = await grnService.CreateDraftFromPoAsync(
            tenantId,
            companyId,
            branchId,
            po.Id,
            defaultWarehouseId: wh.WarehouseId,
            defaultLocationId: loc.WarehouseLocationId);

        // Modify line received quantity to partial 50
        draftGrn.Lines[0].QuantityReceived = 50m;
        await grnService.UpdateDraftAsync(tenantId, companyId, branchId, draftGrn.GoodsReceiptNoteId, new UpdateGoodsReceiptNoteRequest
        {
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = draftGrn.Lines[0].PurchaseOrderItemId,
                    ProductId = product.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 50m
                }
            }
        });

        // Confirm GRN -> Decrements QuantityOnOrder by 50 (leaves 30 on order, 50 on hand)
        await grnService.ConfirmGrnAsync(tenantId, companyId, branchId, draftGrn.GoodsReceiptNoteId);

        var balAfterGrn = await db.InventoryBalances
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseId == wh.WarehouseId && b.ProductId == product.ProductId);
        Assert.NotNull(balAfterGrn);
        Assert.Equal(30m, balAfterGrn.QuantityOnOrder); // 80 - 50 = 30
        Assert.Equal(50m, balAfterGrn.QuantityOnHand);  // 50 on hand

        // Planning calculation: Eligible 50 + OnOrder 30 = 80 Net Available
        var suggestionsAfterGrn = await planningService.GetReplenishmentSuggestionsAsync(tenantId, wh.WarehouseId);
        var sAfterGrn = Assert.Single(suggestionsAfterGrn, x => x.ProductId == product.ProductId);
        Assert.Equal(50m, sAfterGrn.QuantityOnHand);
        Assert.Equal(30m, sAfterGrn.QuantityOnOrder);
        Assert.Equal(80m, sAfterGrn.NetAvailablePlanningStock);
    }

    [Fact]
    public async Task PoLifecycle_CancelApprovedPo_ReversesQuantityOnOrder()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var poService = sp.GetRequiredService<IPurchaseOrderService>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;

        var branch = new global::Domain.Features.MasterData.Branch.Branch
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = $"BR-{Guid.NewGuid():N}"[..10],
            Name = $"Branch {Guid.NewGuid():N}"[..16],
            IsActive = true
        };
        db.Branches.Add(branch);
        var branchId = branch.Id;

        var wh = Warehouse.Create(tenantId, $"WH-{Guid.NewGuid():N}"[..12], "Cancel WH", companyId: companyId, branchId: branchId);
        db.Warehouses.Add(wh);

        var loc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, "LOC-C1", "Storage 1");
        db.WarehouseLocations.Add(loc);

        var cat = Category.Create(tenantId, $"CAT-{Guid.NewGuid():N}"[..12], $"Cancel Cat {Guid.NewGuid():N}"[..18]);
        db.Categories.Add(cat);

        var product = Product.Create(tenantId, $"P-CAN-{Guid.NewGuid():N}"[..12], $"Cancel Prod {Guid.NewGuid():N}"[..18], cat.CategoryId, "PCS");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var po = await poService.CreateOrderAsync(tenantId, companyId, branchId, new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new() { ProductId = product.ProductId, Quantity = 40m, UnitPrice = 10m }
            }
        });

        // Submit & Approve -> on order = 40
        po = await poService.SubmitOrderAsync(tenantId, companyId, branchId, po.Id, Guid.NewGuid(), "Submitter");
        if (po.Status != PurchaseOrderStatus.Approved)
        {
            await poService.ApproveOrderAsync(tenantId, companyId, branchId, po.Id, Guid.NewGuid(), "Approver");
        }

        var bal = await db.InventoryBalances.FirstAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId);
        Assert.Equal(40m, bal.QuantityOnOrder);

        // Cancel -> on order reversed back to 0
        await poService.CancelOrderAsync(tenantId, companyId, branchId, po.Id, Guid.NewGuid(), "Canceller");

        var balAfterCancel = await db.InventoryBalances.FirstAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId);
        Assert.Equal(0m, balAfterCancel.QuantityOnOrder);
    }

    [Fact]
    public async Task ReplenishmentApi_ReturnsSuccess_AndEnforcesPermissions()
    {
        var tenantId = TestConstants.TenantAlphaId;
        var client = _fixture.CreateAuthenticatedClient(
            userId: Guid.NewGuid(),
            username: "planner_user",
            tenantId: tenantId,
            roles: new[] { "InventoryPlanner" },
            permissions: new[] { "INVENTORY.VIEW" });

        var response = await client.GetAsync("/api/inventory/replenishment-suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ReplenishmentSuggestionDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ReplenishmentApi_InvalidWarehouse_ReturnsNotFound()
    {
        var tenantId = TestConstants.TenantAlphaId;
        var client = _fixture.CreateAuthenticatedClient(
            userId: Guid.NewGuid(),
            username: "planner_user",
            tenantId: tenantId,
            roles: new[] { "InventoryPlanner" },
            permissions: new[] { "INVENTORY.VIEW" });

        var nonExistentWhId = Guid.NewGuid();
        var response = await client.GetAsync($"/api/inventory/replenishment-suggestions?warehouseId={nonExistentWhId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(Warehouse Warehouse, WarehouseLocation Location, Category Category, Product Product)>
        SetupWarehouseAndProductAsync(DomainDbContext db, Guid tenantId, TrackingMode trackingMode)
    {
        var wh = Warehouse.Create(tenantId, $"WH-{Guid.NewGuid():N}"[..12], "Test Warehouse");
        db.Warehouses.Add(wh);

        var loc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, "LOC-1", "Default Location");
        db.WarehouseLocations.Add(loc);

        var cat = Category.Create(tenantId, $"CAT-{Guid.NewGuid():N}"[..12], "Test Category");
        db.Categories.Add(cat);

        var product = Product.Create(
            tenantId,
            $"P-{Guid.NewGuid():N}"[..12],
            "Test Planning Product",
            cat.CategoryId,
            "PCS",
            trackingMode: trackingMode);
        db.Products.Add(product);

        await db.SaveChangesAsync();
        return (wh, loc, cat, product);
    }

    private class ApiResponse<T>
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
