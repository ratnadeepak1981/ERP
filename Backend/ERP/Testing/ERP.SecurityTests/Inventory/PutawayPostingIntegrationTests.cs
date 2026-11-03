using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.Inventory.Putaway;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class PutawayPostingIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public PutawayPostingIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task ConfirmPutaway_MultiBinSplit_PostsInternalTransfersAndUpdatesBalances()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // 1. Master Data Setup
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE");
        var binA = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_A");
        var binB = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_B");
        var product = await CreateTestProductAsync(db, tenantId);

        // 2. Simulate Inbound Stock Receipt into Staging (100 units @ 25.00)
        var sourceReceiptId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var receiptTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentIn,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = sourceReceiptId,
            SourceDocumentLineId = receiptLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 100m,
            UnitCost = 25m
        });
        Assert.NotNull(receiptTxn);

        // 3. Create Putaway Draft with Multi-Bin Split (60 units -> Bin A, 40 units -> Bin B)
        var draft = await putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
        {
            SourceDocumentType = PutawaySourceDocumentType.StockReceipt,
            SourceDocumentId = sourceReceiptId,
            SourceDocumentNumber = "SR-TEST-001",
            WarehouseId = warehouse.WarehouseId,
            PutawayDate = DateTime.UtcNow,
            Lines = new List<CreatePutawayLineRequest>
            {
                new()
                {
                    SourceDocumentLineId = receiptLineId,
                    ProductId = product.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 100m,
                    PutawayQuantity = 100m,
                    Allocations = new List<CreatePutawayAllocationRequest>
                    {
                        new()
                        {
                            DestinationWarehouseLocationId = binA.WarehouseLocationId,
                            AllocatedQuantity = 60m
                        },
                        new()
                        {
                            DestinationWarehouseLocationId = binB.WarehouseLocationId,
                            AllocatedQuantity = 40m
                        }
                    }
                }
            }
        });

        Assert.NotNull(draft);
        Assert.Equal(PutawayStatus.Draft, draft.Status);
        Assert.Single(draft.Lines);
        Assert.Equal(2, draft.Lines[0].Allocations.Count);

        // 4. Confirm Putaway -> Posts inventory transfers
        var confirmed = await putawayService.ConfirmPutawayAsync(tenantId, companyId, branchId, draft.PutawayId);
        Assert.NotNull(confirmed);
        Assert.Equal(PutawayStatus.Completed, confirmed.Status);

        // 5. Verify Database Ledger Entries
        var stagingBalance = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, product.ProductId);
        var binABalance = await postingService.GetBalanceAsync(tenantId, binA.WarehouseLocationId, product.ProductId);
        var binBBalance = await postingService.GetBalanceAsync(tenantId, binB.WarehouseLocationId, product.ProductId);

        // Staging: 100 received - 100 put away = 0
        Assert.Equal(0m, stagingBalance?.QuantityOnHand ?? 0m);
        // Bin A: +60
        Assert.Equal(60m, binABalance?.QuantityOnHand ?? 0m);
        // Bin B: +40
        Assert.Equal(40m, binBBalance?.QuantityOnHand ?? 0m);

        // Verify inventory transaction movements
        var transferTxns = await db.InventoryTransactions
            .Where(t => t.TenantId == tenantId && t.ProductId == product.ProductId &&
                        (t.MovementType == InventoryMovementType.InternalTransferOut ||
                         t.MovementType == InventoryMovementType.InternalTransferIn))
            .ToListAsync();

        Assert.Equal(4, transferTxns.Count); // 2 Outflows + 2 Inflows

        var outflows = transferTxns.Where(t => t.MovementType == InventoryMovementType.InternalTransferOut).ToList();
        var inflows = transferTxns.Where(t => t.MovementType == InventoryMovementType.InternalTransferIn).ToList();

        Assert.Equal(2, outflows.Count);
        Assert.Equal(-100m, outflows.Sum(o => o.Quantity)); // Total staging outflow = -100

        Assert.Equal(2, inflows.Count);
        Assert.Equal(100m, inflows.Sum(i => i.Quantity)); // Total bins inflow = +100
        Assert.Contains(inflows, i => i.WarehouseLocationId == binA.WarehouseLocationId && i.Quantity == 60m);
        Assert.Contains(inflows, i => i.WarehouseLocationId == binB.WarehouseLocationId && i.Quantity == 40m);
    }

    [Fact]
    public async Task OverPutaway_ThrowsValidationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE_2");
        var destLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_DEST");
        var product = await CreateTestProductAsync(db, tenantId);

        var sourceReceiptId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var receiptTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentIn,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = sourceReceiptId,
            SourceDocumentLineId = receiptLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 10m
        });

        // Attempting to put away 999 when only 50 was received
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
            {
                SourceDocumentType = PutawaySourceDocumentType.StockReceipt,
                SourceDocumentId = sourceReceiptId,
                SourceDocumentNumber = "SR-OVER-001",
                WarehouseId = warehouse.WarehouseId,
                PutawayDate = DateTime.UtcNow,
                Lines = new List<CreatePutawayLineRequest>
                {
                    new()
                    {
                        SourceDocumentLineId = receiptLineId,
                        ProductId = product.ProductId,
                        SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                        ReceivedQuantity = 50m,
                        PutawayQuantity = 999m,
                        DestinationWarehouseLocationId = destLocation.WarehouseLocationId
                    }
                }
            })
        );
    }

    [Fact]
    public async Task ConfirmPutaway_WhenSourceIsGoodsReceiptWithPurchaseUomConversion_TransfersBaseQuantityAndUpdatesBalances()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        // 1. Setup UOMs and Product with Packaging (1 BOX = 10 PCS)
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);

        string uid = Guid.NewGuid().ToString("N")[..8];
        var product = Product.Create(
            tenantId,
            $"PRD_PA_{uid}",
            $"Putaway Test Product {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);
        await db.Products.AddAsync(product);

        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE_PA");
        var binA = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PA_A");
        var binB = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PA_B");
        await db.SaveChangesAsync();

        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = product.ProductId,
            PackagingCode = $"PKG_PA_{uid}",
            PackagingName = "Box of 10 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 10m
        });

        // 2. Setup Purchase Order and Confirmed GRN for 5 BOX (which converts to 50 PCS)
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO-PA-{uid}", DateTime.UtcNow, supplier?.SupplierId);
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.ProductId,
            Quantity = 5m,
            UnitPrice = 100m,
            UnitOfMeasureId = uomBox.UnitOfMeasureId
        };
        po.Items.Add(poItem);
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var grn = GoodsReceiptNote.Create(tenantId, companyId, branchId, po.Id, $"GRN-PA-{uid}");
        grn.Status = GoodsReceiptNoteStatus.Confirmed;
        string batchNum = $"BATCH-PA-{uid}";
        var expiry = DateTime.UtcNow.AddMonths(6);

        // GRN Line preserves purchase transaction: 5 BOX @ 100/BOX
        var grnLine = GoodsReceiptNoteLine.Create(
            grn.GoodsReceiptNoteId,
            poItem.Id,
            product.ProductId,
            warehouse.WarehouseId,
            stagingLocation.WarehouseLocationId,
            quantityReceived: 5m, // 5 BOX
            unitCost: 100m,
            unitOfMeasureId: uomBox.UnitOfMeasureId,
            batchNumber: batchNum,
            expiryDate: expiry);
        grn.Lines.Add(grnLine);
        await db.GoodsReceiptNotes.AddAsync(grn);
        await db.SaveChangesAsync();

        // Simulate GRN inbound inventory posting: 50 PCS into Staging
        var receiptTxn = await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 50m, // 5 BOX * 10 = 50 PCS in base UOM
            UnitCost = 10m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId,
            BatchNumber = batchNum,
            ExpiryDate = expiry,
            TransactionDate = DateTime.UtcNow
        });
        Assert.NotNull(receiptTxn);

        // Verify staging balance holds 50 PCS
        var initialStagingBal = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, product.ProductId, batchNum);
        Assert.Equal(50m, initialStagingBal?.QuantityOnHand ?? 0m);

        // 3. Inspect Eligible Receipts & Lines
        var eligibleReceipts = await putawayService.GetEligibleSourceReceiptsAsync(tenantId, companyId, branchId, warehouse.WarehouseId);
        var receiptMatch = eligibleReceipts.FirstOrDefault(r => r.Id == grn.GoodsReceiptNoteId);
        Assert.NotNull(receiptMatch);
        Assert.Equal(50m, receiptMatch.TotalQuantity); // Base UOM quantity (50 PCS, not 5 BOX)
        Assert.Equal(50m, receiptMatch.OutstandingQuantity);

        var eligibleLines = await putawayService.GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, PutawaySourceDocumentType.GoodsReceipt, grn.GoodsReceiptNoteId);
        var lineMatch = eligibleLines.FirstOrDefault(l => l.SourceDocumentLineId == grnLine.GoodsReceiptNoteLineId);
        Assert.NotNull(lineMatch);
        Assert.Equal(50m, lineMatch.ReceivedQuantity); // 50 PCS
        Assert.Equal(50m, lineMatch.OutstandingQuantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, lineMatch.UnitOfMeasureId);
        Assert.Equal("PCS", lineMatch.UnitOfMeasure);
        Assert.Equal(batchNum, lineMatch.BatchNumber);

        // 4. Create Putaway Draft in Base UOM with Multi-Bin Split (30 to Bin A, 20 to Bin B)
        var draft = await putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
        {
            SourceDocumentType = PutawaySourceDocumentType.GoodsReceipt,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentNumber = grn.GrnNumber,
            WarehouseId = warehouse.WarehouseId,
            PutawayDate = DateTime.UtcNow,
            Lines = new List<CreatePutawayLineRequest>
            {
                new()
                {
                    SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
                    ProductId = product.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 50m,
                    PutawayQuantity = 50m,
                    BatchNumber = batchNum,
                    ExpiryDate = expiry,
                    Allocations = new List<CreatePutawayAllocationRequest>
                    {
                        new()
                        {
                            DestinationWarehouseLocationId = binA.WarehouseLocationId,
                            AllocatedQuantity = 30m
                        },
                        new()
                        {
                            DestinationWarehouseLocationId = binB.WarehouseLocationId,
                            AllocatedQuantity = 20m
                        }
                    }
                }
            }
        });

        Assert.NotNull(draft);
        Assert.Equal(50m, draft.TotalQuantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, draft.Lines[0].UnitOfMeasureId);
        Assert.Equal(50m, draft.Lines[0].ReceivedQuantity);
        Assert.Equal(50m, draft.Lines[0].PutawayQuantity);

        // 5. Confirm Putaway -> Posts inventory transfers in base UOM
        var confirmed = await putawayService.ConfirmPutawayAsync(tenantId, companyId, branchId, draft.PutawayId);
        Assert.NotNull(confirmed);
        Assert.Equal(PutawayStatus.Completed, confirmed.Status);

        // 6. Verify Balances & Batch Traceability
        var stagingBalAfter = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, product.ProductId, batchNum);
        var binABalAfter = await postingService.GetBalanceAsync(tenantId, binA.WarehouseLocationId, product.ProductId, batchNum);
        var binBBalAfter = await postingService.GetBalanceAsync(tenantId, binB.WarehouseLocationId, product.ProductId, batchNum);

        // Staging: 50 - 50 = 0
        Assert.Equal(0m, stagingBalAfter?.QuantityOnHand ?? 0m);
        // Bin A: +30
        Assert.Equal(30m, binABalAfter?.QuantityOnHand ?? 0m);
        // Bin B: +20
        Assert.Equal(20m, binBBalAfter?.QuantityOnHand ?? 0m);

        // 7. Verify Outstanding quantity is now zero
        var eligibleLinesAfter = await putawayService.GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, PutawaySourceDocumentType.GoodsReceipt, grn.GoodsReceiptNoteId);
        var lineMatchAfter = eligibleLinesAfter.FirstOrDefault(l => l.SourceDocumentLineId == grnLine.GoodsReceiptNoteLineId);
        Assert.NotNull(lineMatchAfter);
        Assert.Equal(0m, lineMatchAfter.OutstandingQuantity);
        Assert.Equal(50m, lineMatchAfter.PreviouslyPutawayQuantity);

        // 8. Verify GRN line purchase transaction details remain intact
        var grnDb = await db.GoodsReceiptNotes.Include(g => g.Lines).FirstAsync(g => g.GoodsReceiptNoteId == grn.GoodsReceiptNoteId);
        var originalLine = grnDb.Lines.First(l => l.GoodsReceiptNoteLineId == grnLine.GoodsReceiptNoteLineId);
        Assert.Equal(5m, originalLine.QuantityReceived); // Original purchase quantity preserved
        Assert.Equal(uomBox.UnitOfMeasureId, originalLine.UnitOfMeasureId); // Original purchase UOM preserved
    }

    [Fact]
    public async Task ConfirmPutaway_PartialPutawayInBaseUom_PreservesRemainingOutstandingQuantityInBaseUom()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);

        string uid = Guid.NewGuid().ToString("N")[..8];
        var product = Product.Create(
            tenantId,
            $"PRD_PART_{uid}",
            $"Partial Putaway Product {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);
        await db.Products.AddAsync(product);

        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE_PART");
        var binA = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PART_A");
        var binB = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PART_B");
        await db.SaveChangesAsync();

        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = product.ProductId,
            PackagingCode = $"PKG_PART_{uid}",
            PackagingName = "Box of 10 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 10m
        });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO-PART-{uid}", DateTime.UtcNow, supplier?.SupplierId);
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.ProductId,
            Quantity = 5m,
            UnitPrice = 100m,
            UnitOfMeasureId = uomBox.UnitOfMeasureId
        };
        po.Items.Add(poItem);
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var grn = GoodsReceiptNote.Create(tenantId, companyId, branchId, po.Id, $"GRN-PART-{uid}");
        grn.Status = GoodsReceiptNoteStatus.Confirmed;
        string batchNum = $"BATCH-PART-{uid}";
        var expiry = DateTime.UtcNow.AddMonths(12);

        var grnLine = GoodsReceiptNoteLine.Create(
            grn.GoodsReceiptNoteId,
            poItem.Id,
            product.ProductId,
            warehouse.WarehouseId,
            stagingLocation.WarehouseLocationId,
            quantityReceived: 5m, // 5 BOX
            unitCost: 100m,
            unitOfMeasureId: uomBox.UnitOfMeasureId,
            batchNumber: batchNum,
            expiryDate: expiry);
        grn.Lines.Add(grnLine);
        await db.GoodsReceiptNotes.AddAsync(grn);
        await db.SaveChangesAsync();

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 50m, // 50 PCS in base UOM
            UnitCost = 10m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId,
            BatchNumber = batchNum,
            ExpiryDate = expiry,
            TransactionDate = DateTime.UtcNow
        });

        // 1. Partial Putaway 1: Put away 20 PCS to Bin A
        var draft1 = await putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
        {
            SourceDocumentType = PutawaySourceDocumentType.GoodsReceipt,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentNumber = grn.GrnNumber,
            WarehouseId = warehouse.WarehouseId,
            PutawayDate = DateTime.UtcNow,
            Lines = new List<CreatePutawayLineRequest>
            {
                new()
                {
                    SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
                    ProductId = product.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 50m,
                    PutawayQuantity = 20m,
                    BatchNumber = batchNum,
                    ExpiryDate = expiry,
                    DestinationWarehouseLocationId = binA.WarehouseLocationId
                }
            }
        });

        var confirmed1 = await putawayService.ConfirmPutawayAsync(tenantId, companyId, branchId, draft1.PutawayId);
        Assert.Equal(PutawayStatus.Completed, confirmed1.Status);

        // Verify balances after partial putaway 1: Staging = 30 PCS, Bin A = 20 PCS
        var stageBal1 = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, product.ProductId, batchNum);
        var binABal1 = await postingService.GetBalanceAsync(tenantId, binA.WarehouseLocationId, product.ProductId, batchNum);
        Assert.Equal(30m, stageBal1?.QuantityOnHand ?? 0m);
        Assert.Equal(20m, binABal1?.QuantityOnHand ?? 0m);

        // Outstanding must be 30 PCS
        var eligibleLinesPart1 = await putawayService.GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, PutawaySourceDocumentType.GoodsReceipt, grn.GoodsReceiptNoteId);
        var linePart1 = eligibleLinesPart1.First(l => l.SourceDocumentLineId == grnLine.GoodsReceiptNoteLineId);
        Assert.Equal(30m, linePart1.OutstandingQuantity);
        Assert.Equal(20m, linePart1.PreviouslyPutawayQuantity);

        // 2. Partial Putaway 2: Put away remaining 30 PCS to Bin B
        var draft2 = await putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
        {
            SourceDocumentType = PutawaySourceDocumentType.GoodsReceipt,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentNumber = grn.GrnNumber,
            WarehouseId = warehouse.WarehouseId,
            PutawayDate = DateTime.UtcNow,
            Lines = new List<CreatePutawayLineRequest>
            {
                new()
                {
                    SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
                    ProductId = product.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 50m,
                    PutawayQuantity = 30m,
                    BatchNumber = batchNum,
                    ExpiryDate = expiry,
                    DestinationWarehouseLocationId = binB.WarehouseLocationId
                }
            }
        });

        var confirmed2 = await putawayService.ConfirmPutawayAsync(tenantId, companyId, branchId, draft2.PutawayId);
        Assert.Equal(PutawayStatus.Completed, confirmed2.Status);

        // Verify balances after partial putaway 2: Staging = 0 PCS, Bin A = 20 PCS, Bin B = 30 PCS
        var stageBal2 = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, product.ProductId, batchNum);
        var binBBal2 = await postingService.GetBalanceAsync(tenantId, binB.WarehouseLocationId, product.ProductId, batchNum);
        Assert.Equal(0m, stageBal2?.QuantityOnHand ?? 0m);
        Assert.Equal(30m, binBBal2?.QuantityOnHand ?? 0m);

        // Outstanding must now be 0 PCS
        var eligibleLinesPart2 = await putawayService.GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, PutawaySourceDocumentType.GoodsReceipt, grn.GoodsReceiptNoteId);
        var linePart2 = eligibleLinesPart2.First(l => l.SourceDocumentLineId == grnLine.GoodsReceiptNoteLineId);
        Assert.Equal(0m, linePart2.OutstandingQuantity);
        Assert.Equal(50m, linePart2.PreviouslyPutawayQuantity);
    }

    [Fact]
    public async Task ConfirmPutaway_MultiLineGrn_AndCostLayerTraceability_VerifiesMatchingAndFifoLayerTransfer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId);

        string uid = Guid.NewGuid().ToString("N")[..8];
        var prod1 = Product.Create(
            tenantId,
            $"PRD_ML1_{uid}",
            $"MultiLine Prod 1 {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);

        var prod2 = Product.Create(
            tenantId,
            $"PRD_ML2_{uid}",
            $"MultiLine Prod 2 {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);

        await db.Products.AddRangeAsync(prod1, prod2);

        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE_ML");
        var binA = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ML_A");
        var binB = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ML_B");
        await db.SaveChangesAsync();

        // 1 BOX = 10 PCS for prod1
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod1.ProductId,
            PackagingCode = $"PKG_ML_{uid}",
            PackagingName = "Box of 10 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 10m
        });

        // Setup PO with 2 lines: Line 1 = 3 BOX prod1, Line 2 = 25 PCS prod2
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO-ML-{uid}", DateTime.UtcNow, supplier?.SupplierId);
        var poItem1 = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = prod1.ProductId,
            Quantity = 3m,
            UnitPrice = 100m,
            UnitOfMeasureId = uomBox.UnitOfMeasureId
        };
        var poItem2 = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = prod2.ProductId,
            Quantity = 25m,
            UnitPrice = 15m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId
        };
        po.Items.Add(poItem1);
        po.Items.Add(poItem2);
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        // Confirmed GRN with 2 lines
        var grn = GoodsReceiptNote.Create(tenantId, companyId, branchId, po.Id, $"GRN-ML-{uid}");
        grn.Status = GoodsReceiptNoteStatus.Confirmed;
        string batch1 = $"B-ML1-{uid}";
        string batch2 = $"B-ML2-{uid}";
        var expiry1 = DateTime.UtcNow.AddMonths(10);
        var expiry2 = DateTime.UtcNow.AddMonths(20);

        var grnLine1 = GoodsReceiptNoteLine.Create(
            grn.GoodsReceiptNoteId,
            poItem1.Id,
            prod1.ProductId,
            warehouse.WarehouseId,
            stagingLocation.WarehouseLocationId,
            quantityReceived: 3m, // 3 BOX
            unitCost: 100m,
            unitOfMeasureId: uomBox.UnitOfMeasureId,
            batchNumber: batch1,
            expiryDate: expiry1);

        var grnLine2 = GoodsReceiptNoteLine.Create(
            grn.GoodsReceiptNoteId,
            poItem2.Id,
            prod2.ProductId,
            warehouse.WarehouseId,
            stagingLocation.WarehouseLocationId,
            quantityReceived: 25m, // 25 PCS (Base UOM directly)
            unitCost: 15m,
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            batchNumber: batch2,
            expiryDate: expiry2);

        grn.Lines.Add(grnLine1);
        grn.Lines.Add(grnLine2);
        await db.GoodsReceiptNotes.AddAsync(grn);
        await db.SaveChangesAsync();

        // Inbound postings:
        // Line 1: 30 PCS prod1 into staging
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentLineId = grnLine1.GoodsReceiptNoteLineId,
            MovementSequence = 1,
            ProductId = prod1.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 30m,
            UnitCost = 10m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId,
            BatchNumber = batch1,
            ExpiryDate = expiry1,
            TransactionDate = DateTime.UtcNow
        });

        // Line 2: 25 PCS prod2 into staging
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentLineId = grnLine2.GoodsReceiptNoteLineId,
            MovementSequence = 2,
            ProductId = prod2.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 25m,
            UnitCost = 15m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId,
            BatchNumber = batch2,
            ExpiryDate = expiry2,
            TransactionDate = DateTime.UtcNow
        });

        // Verify eligible lines resolution:
        var lines = await putawayService.GetEligibleReceiptLinesAsync(tenantId, companyId, branchId, PutawaySourceDocumentType.GoodsReceipt, grn.GoodsReceiptNoteId);
        Assert.Equal(2, lines.Count);

        var match1 = lines.First(l => l.SourceDocumentLineId == grnLine1.GoodsReceiptNoteLineId);
        Assert.Equal(30m, match1.ReceivedQuantity); // Converted base quantity
        Assert.Equal(30m, match1.OutstandingQuantity);
        Assert.Equal(batch1, match1.BatchNumber);

        var match2 = lines.First(l => l.SourceDocumentLineId == grnLine2.GoodsReceiptNoteLineId);
        Assert.Equal(25m, match2.ReceivedQuantity); // Base UOM quantity untouched
        Assert.Equal(25m, match2.OutstandingQuantity);
        Assert.Equal(batch2, match2.BatchNumber);

        // Putaway Draft: prod1 -> Bin A (30 PCS), prod2 -> Bin B (25 PCS)
        var draft = await putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
        {
            SourceDocumentType = PutawaySourceDocumentType.GoodsReceipt,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentNumber = grn.GrnNumber,
            WarehouseId = warehouse.WarehouseId,
            PutawayDate = DateTime.UtcNow,
            Lines = new List<CreatePutawayLineRequest>
            {
                new()
                {
                    SourceDocumentLineId = grnLine1.GoodsReceiptNoteLineId,
                    ProductId = prod1.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 30m,
                    PutawayQuantity = 30m,
                    BatchNumber = batch1,
                    ExpiryDate = expiry1,
                    DestinationWarehouseLocationId = binA.WarehouseLocationId
                },
                new()
                {
                    SourceDocumentLineId = grnLine2.GoodsReceiptNoteLineId,
                    ProductId = prod2.ProductId,
                    SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                    ReceivedQuantity = 25m,
                    PutawayQuantity = 25m,
                    BatchNumber = batch2,
                    ExpiryDate = expiry2,
                    DestinationWarehouseLocationId = binB.WarehouseLocationId
                }
            }
        });

        var confirmed = await putawayService.ConfirmPutawayAsync(tenantId, companyId, branchId, draft.PutawayId);
        Assert.Equal(PutawayStatus.Completed, confirmed.Status);

        // Verify Staging depleted for both products
        var stage1 = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, prod1.ProductId, batch1);
        var stage2 = await postingService.GetBalanceAsync(tenantId, stagingLocation.WarehouseLocationId, prod2.ProductId, batch2);
        Assert.Equal(0m, stage1?.QuantityOnHand ?? 0m);
        Assert.Equal(0m, stage2?.QuantityOnHand ?? 0m);

        // Verify Bin A and Bin B balances
        var binABal = await postingService.GetBalanceAsync(tenantId, binA.WarehouseLocationId, prod1.ProductId, batch1);
        var binBBal = await postingService.GetBalanceAsync(tenantId, binB.WarehouseLocationId, prod2.ProductId, batch2);
        Assert.Equal(30m, binABal?.QuantityOnHand ?? 0m);
        Assert.Equal(25m, binBBal?.QuantityOnHand ?? 0m);

        // Verify FIFO Cost Layers transferred from staging to destination locations
        var costLayersProd1 = await db.InventoryCostLayers
            .Where(c => c.TenantId == tenantId && c.ProductId == prod1.ProductId && c.BatchNumber == batch1)
            .ToListAsync();
        Assert.Contains(costLayersProd1, c => c.WarehouseLocationId == binA.WarehouseLocationId && c.RemainingQuantity == 30m);
        Assert.All(costLayersProd1.Where(c => c.WarehouseLocationId == stagingLocation.WarehouseLocationId), c => Assert.Equal(0m, c.RemainingQuantity));

        var costLayersProd2 = await db.InventoryCostLayers
            .Where(c => c.TenantId == tenantId && c.ProductId == prod2.ProductId && c.BatchNumber == batch2)
            .ToListAsync();
        Assert.Contains(costLayersProd2, c => c.WarehouseLocationId == binB.WarehouseLocationId && c.RemainingQuantity == 25m);
        Assert.All(costLayersProd2.Where(c => c.WarehouseLocationId == stagingLocation.WarehouseLocationId), c => Assert.Equal(0m, c.RemainingQuantity));
    }

    [Fact]
    public async Task ConfirmPutaway_OverPutawayAgainstBaseUomQuantity_ThrowsValidationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var putawayService = sp.GetRequiredService<IPutawayService>();
        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId);

        string uid = Guid.NewGuid().ToString("N")[..8];
        var product = Product.Create(
            tenantId,
            $"PRD_OVER_{uid}",
            $"Over Putaway Product {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);
        await db.Products.AddAsync(product);

        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var stagingLocation = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "STAGE_OVER");
        var binA = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_OVER_A");
        await db.SaveChangesAsync();

        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = product.ProductId,
            PackagingCode = $"PKG_OVER_{uid}",
            PackagingName = "Box of 10 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 10m
        });

        // 2 BOX received -> 20 PCS in base UOM
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO-OVER-{uid}", DateTime.UtcNow, supplier?.SupplierId);
        var poItem = new PurchaseOrderItem
        {
            Id = Guid.NewGuid(),
            PurchaseOrderId = po.Id,
            ProductId = product.ProductId,
            Quantity = 2m,
            UnitPrice = 100m,
            UnitOfMeasureId = uomBox.UnitOfMeasureId
        };
        po.Items.Add(poItem);
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var grn = GoodsReceiptNote.Create(tenantId, companyId, branchId, po.Id, $"GRN-OVER-{uid}");
        grn.Status = GoodsReceiptNoteStatus.Confirmed;
        string batchNum = $"BATCH-OVER-{uid}";
        var expiry = DateTime.UtcNow.AddMonths(6);

        var grnLine = GoodsReceiptNoteLine.Create(
            grn.GoodsReceiptNoteId,
            poItem.Id,
            product.ProductId,
            warehouse.WarehouseId,
            stagingLocation.WarehouseLocationId,
            quantityReceived: 2m, // 2 BOX
            unitCost: 100m,
            unitOfMeasureId: uomBox.UnitOfMeasureId,
            batchNumber: batchNum,
            expiryDate: expiry);
        grn.Lines.Add(grnLine);
        await db.GoodsReceiptNotes.AddAsync(grn);
        await db.SaveChangesAsync();

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = grn.GoodsReceiptNoteId,
            SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = stagingLocation.WarehouseLocationId,
            Quantity = 20m, // 20 PCS
            UnitCost = 10m,
            UnitOfMeasureId = uomPcs.UnitOfMeasureId,
            BatchNumber = batchNum,
            ExpiryDate = expiry,
            TransactionDate = DateTime.UtcNow
        });

        // Attempting to put away 21 PCS when only 20 PCS exists in base UOM
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            putawayService.CreateDraftAsync(tenantId, companyId, branchId, new CreatePutawayDraftRequest
            {
                SourceDocumentType = PutawaySourceDocumentType.GoodsReceipt,
                SourceDocumentId = grn.GoodsReceiptNoteId,
                SourceDocumentNumber = grn.GrnNumber,
                WarehouseId = warehouse.WarehouseId,
                PutawayDate = DateTime.UtcNow,
                Lines = new List<CreatePutawayLineRequest>
                {
                    new()
                    {
                        SourceDocumentLineId = grnLine.GoodsReceiptNoteLineId,
                        ProductId = product.ProductId,
                        SourceWarehouseLocationId = stagingLocation.WarehouseLocationId,
                        ReceivedQuantity = 20m,
                        PutawayQuantity = 21m, // Over by 1 PCS
                        DestinationWarehouseLocationId = binA.WarehouseLocationId
                    }
                }
            })
        );
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", city: "Colombo");
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId, string prefix)
    {
        string code = $"{prefix}_{Guid.NewGuid():N}"[..18];
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

    private static async Task<Product> CreateTestProductAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"PRD_{Guid.NewGuid():N}"[..18];
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        Guid catId = cat?.CategoryId ?? Guid.NewGuid();

        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            catId,
            unitOfMeasure: "PCS",
            trackingMode: TrackingMode.None,
            isStockTracked: true);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
