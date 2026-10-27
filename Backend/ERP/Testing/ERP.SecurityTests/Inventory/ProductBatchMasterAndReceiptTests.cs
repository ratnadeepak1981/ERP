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
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class ProductBatchMasterAndReceiptTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public ProductBatchMasterAndReceiptTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ValidBatchReceipt_AndReuseOfExistingBatch_SucceedsAndPreservesDates()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        // 1. Initial inflow creates ProductBatch record
        var txn1 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 15m,
            BatchNumber = batchNo,
            ManufacturingDate = mfgDate,
            ExpiryDate = expDate
        });

        Assert.Equal(batchNo, txn1.BatchNumber);
        Assert.Equal(expDate, txn1.ExpiryDate);

        // Verify batch in database
        var savedBatch = await db.ProductBatches.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(savedBatch);
        Assert.Equal(mfgDate.Date, savedBatch.ManufacturingDate);
        Assert.Equal(expDate.Date, savedBatch.ExpiryDate);
        Assert.True(savedBatch.IsActive);

        // 2. Subsequent receipt of existing batch with identical dates succeeds and reuses batch
        var txn2 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 30m,
            UnitCost = 15m,
            BatchNumber = batchNo,
            ManufacturingDate = mfgDate,
            ExpiryDate = expDate
        });

        Assert.Equal(batchNo, txn2.BatchNumber);

        // 3. Receipt of existing batch with omitted dates succeeds and inherits authoritative dates from batch master
        var txn3 = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 15m,
            BatchNumber = batchNo
            // Dates omitted
        });

        Assert.Equal(batchNo, txn3.BatchNumber);
        Assert.Equal(expDate, txn3.ExpiryDate);

        // Verify stock balance accumulated
        var bal = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, product.ProductId, batchNo);
        Assert.NotNull(bal);
        Assert.Equal(100m, bal.QuantityOnHand);
    }

    [Fact]
    public async Task DuplicateBatchIdentity_AcrossDifferentProducts_ThrowsMismatchException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B2");

        var productA = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "MED_A");
        var productB = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "MED_B");

        string sharedBatchNo = $"SHARED-LOT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // 1. Receive batch under Product A
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = productA.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m,
            BatchNumber = sharedBatchNo
        });

        // 2. Attempt to receive same batch under Product B -> Must fail with cross-product mismatch
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = productB.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m,
            BatchNumber = sharedBatchNo
        }));

        Assert.Contains("already associated with another product", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConflictingExpiryOrManufacturingDates_ThrowsException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B3");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch);

        string batchNo = $"LOT-CONF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var originalMfg = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalExp = new DateTime(2027, 8, 31, 0, 0, 0, DateTimeKind.Utc);

        // 1. Initial receipt
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 25m,
            BatchNumber = batchNo,
            ManufacturingDate = originalMfg,
            ExpiryDate = originalExp
        });

        // 2. Inflow with conflicting Expiry Date -> throws
        var conflictingExp = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var exExp = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 15m,
            BatchNumber = batchNo,
            ManufacturingDate = originalMfg,
            ExpiryDate = conflictingExp
        }));

        Assert.Contains("Conflicting expiry date", exExp.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Inflow with conflicting Manufacturing Date -> throws
        var conflictingMfg = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var exMfg = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 15m,
            BatchNumber = batchNo,
            ManufacturingDate = conflictingMfg,
            ExpiryDate = originalExp
        }));

        Assert.Contains("Conflicting manufacturing date", exMfg.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequiredBatchInformation_ForTrackedProducts_Enforced()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B4");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch);

        // Missing batch number on batch-tracked item
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m
            // BatchNumber is null
        }));

        Assert.Contains("BatchNumber is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UntrackedAndSerialTrackedProducts_CorrectBehaviour()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B5");

        // 1. Untracked product: providing a batch number throws
        var untrackedProduct = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var exNone = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = untrackedProduct.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m,
            BatchNumber = "ILLEGAL-BATCH"
        }));
        Assert.Contains("does not track batches", exNone.Message, StringComparison.OrdinalIgnoreCase);

        // Untracked product: posting without batch succeeds
        var validNoneTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = untrackedProduct.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 10m
        });
        Assert.Null(validNoneTxn.BatchNumber);

        // 2. Serial-tracked product: must not have batch number
        var serialProduct = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial);
        var exSerial = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            ProductId = serialProduct.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Quantity = 1m,
            SerialNumber = "SN-12345",
            BatchNumber = "ILLEGAL-BATCH"
        }));
        Assert.Contains("does not track batches", exSerial.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransactionRollback_WhenBatchValidationFails_OnGRNConfirmation()
    {
        // Tests that if batch validation throws during GRN confirmation, the entire transaction rolls back cleanly
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE", "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B6");
        var productA = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "PROD_A");
        var productB = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "PROD_B");

        // Pre-register batch BATCH-RESERVED for Product B
        string reservedBatch = $"RSV-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var existingBatchB = ProductBatch.Create(tenantId, productB.ProductId, reservedBatch);
        await db.ProductBatches.AddAsync(existingBatchB);
        await db.SaveChangesAsync();

        // Create PO with Product A
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = TestConstants.CompanyAId,
            BranchId = TestConstants.BranchA1Id,
            OrderNumber = $"PO-BATCH-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            OrderDate = DateTime.UtcNow,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 100m,
            Items = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productA.ProductId,
                    Quantity = 10m,
                    UnitPrice = 10m,
                    LineTotal = 100m
                }
            }
        };
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Create GRN Draft for Product A using the reserved batch of Product B
        var grnRes = await adminClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes",
            new CreateGoodsReceiptNoteRequest
            {
                PurchaseOrderId = po.Id,
                GrnNumber = $"GRN-ROLL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Lines = new List<CreateGoodsReceiptNoteLineRequest>
                {
                    new CreateGoodsReceiptNoteLineRequest
                    {
                        PurchaseOrderItemId = po.Items[0].Id,
                        ProductId = productA.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityReceived = 10m,
                        BatchNumber = reservedBatch // Mismatch!
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, grnRes.StatusCode);
        var grnData = await grnRes.ReadDataJsonElementAsync();
        var grnId = grnData.GetProperty("goodsReceiptNoteId").GetGuid();

        // Act: Confirm GRN -> Must fail with conflict due to batch mismatch
        var confirmRes = await adminClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes/{grnId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.Conflict, confirmRes.StatusCode);
        var errBody = await confirmRes.Content.ReadAsStringAsync();
        Assert.Contains("already associated with another product", errBody, StringComparison.OrdinalIgnoreCase);

        // Verify Rollback: GRN remains Draft, PO line has 0 received, and no stock balance was posted
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var grnAfter = await verifyDb.GoodsReceiptNotes.FirstOrDefaultAsync(g => g.GoodsReceiptNoteId == grnId);
        Assert.NotNull(grnAfter);
        Assert.Equal(GoodsReceiptNoteStatus.Draft, grnAfter.Status);

        var poItemAfter = await verifyDb.PurchaseOrderItems.FirstOrDefaultAsync(i => i.Id == po.Items[0].Id);
        Assert.NotNull(poItemAfter);
        Assert.Equal(0m, poItemAfter.ReceivedQuantity);

        var balances = await verifyDb.InventoryBalances.Where(b => b.WarehouseLocationId == loc.WarehouseLocationId && b.ProductId == productA.ProductId).ToListAsync();
        Assert.Empty(balances);
    }

    [Fact]
    public async Task ExistingGRNPosting_AndValuationRegressions_Preserved()
    {
        // Tests complete GRN confirmation for batch-tracked product: stock balances, cost layers, and ProductBatch created
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE", "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B7");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "VAL_PROD");

        string batchNo = $"VAL-BATCH-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var expDate = new DateTime(2027, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        var mfgDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = TestConstants.CompanyAId,
            BranchId = TestConstants.BranchA1Id,
            OrderNumber = $"PO-VAL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            OrderDate = DateTime.UtcNow,
            Status = PurchaseOrderStatus.Approved,
            TotalAmount = 500m,
            Items = new List<PurchaseOrderItem>
            {
                new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    Quantity = 20m,
                    UnitPrice = 25m,
                    LineTotal = 500m
                }
            }
        };
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Create GRN
        var grnRes = await adminClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes",
            new CreateGoodsReceiptNoteRequest
            {
                PurchaseOrderId = po.Id,
                GrnNumber = $"GRN-VAL-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                Lines = new List<CreateGoodsReceiptNoteLineRequest>
                {
                    new CreateGoodsReceiptNoteLineRequest
                    {
                        PurchaseOrderItemId = po.Items[0].Id,
                        ProductId = product.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityReceived = 20m,
                        BatchNumber = batchNo,
                        ManufacturingDate = mfgDate,
                        ExpiryDate = expDate
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, grnRes.StatusCode);
        var grnData = await grnRes.ReadDataJsonElementAsync();
        var grnId = grnData.GetProperty("goodsReceiptNoteId").GetGuid();

        // Confirm GRN
        var confirmRes = await adminClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes/{grnId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // Verify Database state
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        // 1. Batch Master
        var batch = await verifyDb.ProductBatches.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(batch);
        Assert.Equal(expDate.Date, batch.ExpiryDate);
        Assert.Equal(mfgDate.Date, batch.ManufacturingDate);

        // 2. Inventory Balance
        var balance = await verifyDb.InventoryBalances.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == loc.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(balance);
        Assert.Equal(20m, balance.QuantityOnHand);
        Assert.Equal(20m, balance.QuantityAvailable);

        // 3. Cost Layer
        var costLayer = await verifyDb.InventoryCostLayers.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ProductId == product.ProductId && c.BatchNumber == batchNo);
        Assert.NotNull(costLayer);
        Assert.Equal(20m, costLayer.OriginalQuantity);
        Assert.Equal(20m, costLayer.RemainingQuantity);
        Assert.Equal(25m, costLayer.UnitCost);
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..8];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", city: "Colombo");
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId, string codePrefix)
    {
        string code = $"{codePrefix}_{Guid.NewGuid():N}"[..10];
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
        string code = $"{prefix}_{Guid.NewGuid():N}"[..10];
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
