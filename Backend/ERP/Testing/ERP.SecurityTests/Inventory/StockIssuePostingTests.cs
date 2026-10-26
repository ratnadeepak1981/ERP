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

public class StockIssuePostingTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockIssuePostingTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task PostStockIssue_Successful_UpdatesLedgerAndBalance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ISSUE");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // 1. Inbound stock receipt (+10)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
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
        });

        // 2. Post Stock Issue (-4)
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Remarks = "Testing stock issue",
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 4m
                }
            }
        };

        var result = await inventoryService.CreateStockIssueAsync(tenantId, issueRequest);

        Assert.NotNull(result);
        Assert.Single(result);
        var issueDto = result[0];
        Assert.Equal(4m, issueDto.Quantity);
        Assert.Equal(StockIssueStatus.Posted, issueDto.Status);
        Assert.Equal("Posted", issueDto.StatusName);
        Assert.Equal(25m, issueDto.UnitCost);
        Assert.Equal(100m, issueDto.TotalCost);

        // Verify ledger transaction record
        var tx = await db.InventoryTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.InventoryTransactionId == issueDto.StockIssueId);
        Assert.NotNull(tx);
        Assert.Equal(-4m, tx.Quantity);
        Assert.Equal(25m, tx.UnitCost);
        Assert.Equal(100m, tx.TotalCost);

        // 3. Balance verification
        var balance = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(6m, balance.QuantityOnHand);
        Assert.Equal(6m, balance.QuantityAvailable);
    }

    [Fact]
    public async Task PostStockIssue_MultiLine_Successful()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var loc1 = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_MULTI1");
        var loc2 = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_MULTI2");
        var prodA = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var prodB = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Receipt Prod A (15) and Prod B (20)
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = prodA.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc1.WarehouseLocationId,
            Quantity = 15m,
            UnitCost = 10m
        });

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = prodB.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = loc2.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 15m
        });

        // Issue 5 of Prod A and 8 of Prod B
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.Manufacturing,
            Remarks = "Production batch",
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = prodA.ProductId, WarehouseLocationId = loc1.WarehouseLocationId, Quantity = 5m },
                new() { ProductId = prodB.ProductId, WarehouseLocationId = loc2.WarehouseLocationId, Quantity = 8m }
            }
        };

        var result = await inventoryService.CreateStockIssueAsync(tenantId, issueRequest);

        Assert.Equal(2, result.Count);
        Assert.Equal(5m, result[0].Quantity);
        Assert.Equal(8m, result[1].Quantity);

        var balA = await postingService.GetBalanceAsync(tenantId, loc1.WarehouseLocationId, prodA.ProductId);
        Assert.Equal(10m, balA?.QuantityOnHand);

        var balB = await postingService.GetBalanceAsync(tenantId, loc2.WarehouseLocationId, prodB.ProductId);
        Assert.Equal(12m, balB?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_InsufficientStock_ThrowsAndLeavesBalanceIntact()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INSUF");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Inbound: 5 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
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
            UnitCost = 30m
        });

        // Attempt issue of 8 units
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 8m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueRequest));
        Assert.Contains("Insufficient available stock", ex.Message);

        // Verify balance remained at 5 units
        var bal = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(5m, bal?.QuantityOnHand);
        Assert.Equal(5m, bal?.QuantityAvailable);
    }

    [Fact]
    public async Task PostStockIssue_MultiLineAggregatedStockExceeded_ThrowsAndLeavesBalanceIntact()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_AGGR");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Inbound: 10 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
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
            UnitCost = 20m
        });

        // Line 1 requests 6, Line 2 requests 5 (total 11 > 10)
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 6m },
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueRequest));
        Assert.Contains("Insufficient available stock", ex.Message);

        // Balance untouched
        var bal = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(10m, bal?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_RepeatedPosting_DuplicateRejectionPreventsDoubleDeduction()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_IDEMP");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        // Inbound: 10 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
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
            UnitCost = 40m
        });

        string sourceRef = $"REF_REPEATED_{Guid.NewGuid():N}";
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            SourceDocumentReference = sourceRef,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 3m }
            }
        };

        // 1st attempt: Succeeds
        var result1 = await inventoryService.CreateStockIssueAsync(tenantId, issueRequest);
        Assert.Single(result1);

        var balAfterFirst = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(7m, balAfterFirst?.QuantityOnHand);

        // 2nd attempt: Same request repeated
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueRequest));
        Assert.Contains("already been posted", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Crucial verification: balance must still be 7, NOT 4!
        var balAfterSecond = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.Equal(7m, balAfterSecond?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_InvalidQuantities_ThrowsArgumentException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INVQTY");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        var requestZero = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 0m }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => inventoryService.CreateStockIssueAsync(tenantId, requestZero));

        var requestNegative = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = -2.5m }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => inventoryService.CreateStockIssueAsync(tenantId, requestNegative));
    }

    [Fact]
    public async Task PostStockIssue_LocationFromDifferentWarehouse_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse1 = await CreateTestWarehouseAsync(db, tenantId);
        var warehouse2 = await CreateTestWarehouseAsync(db, tenantId);
        var locInWarehouse2 = await CreateTestLocationAsync(db, tenantId, warehouse2.WarehouseId, "BIN_WH2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        var request = new CreateStockIssueRequest
        {
            WarehouseId = warehouse1.WarehouseId, // Mismatched warehouse!
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = locInWarehouse2.WarehouseLocationId, Quantity = 1m }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, request));
        Assert.Contains("does not belong to warehouse", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_TransactionRollback_WhenFailureOccursPartwayThrough_RestoresBalance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ROLL");
        var prodA = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var prodNonStocked = await CreateTestProductAsync(db, tenantId, TrackingMode.None, isStockTracked: false);

        // Inbound 10 of Prod A
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = prodA.ProductId,
            WarehouseId = warehouse.WarehouseId,
            WarehouseLocationId = location.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 50m
        });

        // Issue Line 1 (Prod A, valid) + Line 2 (Prod NonStocked, invalid because not stock tracked)
        var issueRequest = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = prodA.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 4m },
                new() { ProductId = prodNonStocked.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 2m }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueRequest));

        // Line 1 MUST NOT have been committed. Prod A balance must still be exactly 10!
        var balA = await postingService.GetBalanceAsync(tenantId, location.WarehouseLocationId, prodA.ProductId);
        Assert.Equal(10m, balA?.QuantityOnHand);
        Assert.Equal(10m, balA?.QuantityAvailable);

        // Ledger must not contain any issue transaction for this request
        var txnCount = await db.InventoryTransactions.AsNoTracking().CountAsync(t => t.TenantId == tenantId && t.ProductId == prodA.ProductId && t.Quantity < 0);
        Assert.Equal(0, txnCount);
    }

    [Fact]
    public async Task PostStockIssue_TenantIsolation_CannotIssueOtherTenantsStock()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantAlpha = TestConstants.TenantAlphaId;
        var tenantBeta = TestConstants.TenantBetaId;

        var whAlpha = await CreateTestWarehouseAsync(db, tenantAlpha);
        var locAlpha = await CreateTestLocationAsync(db, tenantAlpha, whAlpha.WarehouseId, "BIN_T_ALPHA");
        var prodAlpha = await CreateTestProductAsync(db, tenantAlpha, TrackingMode.None);

        // Tenant Beta tries to issue Tenant Alpha's warehouse and product
        var crossTenantRequest = new CreateStockIssueRequest
        {
            WarehouseId = whAlpha.WarehouseId,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = prodAlpha.ProductId, WarehouseLocationId = locAlpha.WarehouseLocationId, Quantity = 1m }
            }
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => inventoryService.CreateStockIssueAsync(tenantBeta, crossTenantRequest));
    }

    [Fact]
    public async Task PostStockIssue_PurposeOther_MappedAndRetrievable()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_OTHER");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
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
            UnitCost = 15m
        });

        var request = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.Other,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 3m }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, request);
        Assert.Single(results);
        var issue = results[0];
        Assert.Equal(StockIssuePurpose.Other, issue.Purpose);
        Assert.Equal("Other Authorized Purpose", issue.PurposeName);

        // Query by purpose = Other
        var queryResults = await inventoryService.GetStockIssuesAsync(tenantId, warehouse.WarehouseId, StockIssuePurpose.Other);
        Assert.Contains(queryResults, q => q.StockIssueId == issue.StockIssueId && q.Purpose == StockIssuePurpose.Other);

        // Get by ID
        var byId = await inventoryService.GetStockIssueByIdAsync(tenantId, issue.StockIssueId);
        Assert.NotNull(byId);
        Assert.Equal(StockIssuePurpose.Other, byId.Purpose);
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18];
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
        string code = $"{prefix}_{Guid.NewGuid():N}"[..18];
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
        TrackingMode trackingMode,
        bool isStockTracked = true)
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
            trackingMode: trackingMode,
            isStockTracked: isStockTracked);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
