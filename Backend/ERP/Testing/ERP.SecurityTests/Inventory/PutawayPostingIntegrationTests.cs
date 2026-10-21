using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.Inventory.Putaway;
using Domain.Features.MasterData.Product;
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
