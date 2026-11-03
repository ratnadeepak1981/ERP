using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Inventory;

public class StockIssueBatchIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockIssueBatchIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    #region Batch Tracking Tests (1 - 8)

    [Fact]
    public async Task PostStockIssue_ValidBatchIssue_DeductsBatchBalanceAndCostLayer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ISSUE_B1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_P1");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = DateTime.UtcNow.AddMonths(-2).Date;
        var expDate = DateTime.UtcNow.AddMonths(10).Date;

        // 1. Inbound receipt: 10 units @ 20.00
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 10m,
                    UnitCost = 20m,
                    BatchNumber = batchNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate
                }
            }
        });

        // 2. Issue 4 units
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Remarks = "Valid batch issue test",
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 4m,
                    BatchNumber = batchNo
                }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(4m, dto.Quantity);
        Assert.Equal(20m, dto.UnitCost);
        Assert.Equal(80m, dto.TotalCost);
        Assert.Equal(batchNo, dto.BatchNumber);

        // Verify transaction
        var tx = await db.InventoryTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.InventoryTransactionId == dto.StockIssueId);
        Assert.NotNull(tx);
        Assert.Equal(-4m, tx.Quantity);
        Assert.Equal(20m, tx.UnitCost);
        Assert.Equal(batchNo, tx.BatchNumber);

        // Verify balance
        var balance = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo && b.WarehouseLocationId == location.WarehouseLocationId);
        Assert.NotNull(balance);
        Assert.Equal(6m, balance.QuantityOnHand);
        Assert.Equal(6m, balance.QuantityAvailable);

        // Verify cost layer remaining
        var layer = await db.InventoryCostLayers.AsNoTracking().FirstOrDefaultAsync(l =>
            l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == batchNo);
        Assert.NotNull(layer);
        Assert.Equal(6m, layer.RemainingQuantity);
        Assert.False(layer.IsExhausted);
    }

    [Fact]
    public async Task PostStockIssue_MissingBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_MB");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_MB");

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 2m,
                    BatchNumber = "" // Missing!
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Batch number is required", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_WrongProductBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_WPB");
        var prodA = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "PROD_A");
        var prodB = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "PROD_B");

        string batchNo = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Inbound for Prod A
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = prodA.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 10m,
                    UnitCost = 15m,
                    BatchNumber = batchNo
                }
            }
        });

        // Attempt issue of Prod B using Prod A's batch
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = prodB.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 2m,
                    BatchNumber = batchNo
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("already associated with another product", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_InactiveBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INACT");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_INACT");

        string batchNo = $"LOT-INACT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // 1. Inbound receipt
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 10m,
                    UnitCost = 25m,
                    BatchNumber = batchNo
                }
            }
        });

        // 2. Deactivate batch
        var registeredBatch = await db.ProductBatches.FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(registeredBatch);
        registeredBatch.Deactivate();
        await db.SaveChangesAsync();

        // 3. Attempt issue from inactive batch
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 3m,
                    BatchNumber = batchNo
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("is inactive and cannot be issued", ex.Message);

        // Verify balance remained intact at 10
        var balance = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(balance);
        Assert.Equal(10m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_ExpiredBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_EXP");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_EXP");

        string batchNo = $"LOT-EXP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var pastDate = DateTime.UtcNow.AddDays(-10).Date;

        // Inbound receipt with past expiry date
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 10m,
                    UnitCost = 25m,
                    BatchNumber = batchNo,
                    ExpiryDate = pastDate
                }
            }
        });

        // Attempt issue
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 2m,
                    BatchNumber = batchNo
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("expired on", ex.Message);

        // Balance remained intact
        var balance = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(balance);
        Assert.Equal(10m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_InsufficientSelectedBatchQuantity_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INSUF_B");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_INSUF");

        string batch1 = $"LOT-1-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batch2 = $"LOT-2-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Inbound: LOT-1 has 5, LOT-2 has 10 (total 15)
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, UnitCost = 10m, BatchNumber = batch1 },
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 10m, BatchNumber = batch2 }
            }
        });

        // Attempt to issue 8 units from LOT-1 (which only has 5)
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 8m, BatchNumber = batch1 }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Insufficient available stock", ex.Message);

        // Both batch balances intact
        var b1 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch1);
        Assert.Equal(5m, b1?.QuantityOnHand);

        var b2 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch2);
        Assert.Equal(10m, b2?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_PartialBatchDepletion()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PART_B");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_PART");

        string batchNo = $"LOT-PART-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 15m, BatchNumber = batchNo }
            }
        });

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 3m, BatchNumber = batchNo }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);

        var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.Equal(7m, bal?.QuantityOnHand);

        var layer = await db.InventoryCostLayers.AsNoTracking().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == batchNo);
        Assert.Equal(7m, layer?.RemainingQuantity);
        Assert.False(layer?.IsExhausted);
    }

    [Fact]
    public async Task PostStockIssue_FullBatchDepletion()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_FULL_B");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_FULL");

        string batchNo = $"LOT-FULL-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, UnitCost = 18m, BatchNumber = batchNo }
            }
        });

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, BatchNumber = batchNo }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);

        var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.Equal(0m, bal?.QuantityOnHand);

        var layer = await db.InventoryCostLayers.AsNoTracking().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == batchNo);
        Assert.Equal(0m, layer?.RemainingQuantity);
        Assert.True(layer?.IsExhausted);
    }

    #endregion

    #region Serial Tracking Tests (9 - 13)

    [Fact]
    public async Task PostStockIssue_ValidSerialIssue_DeductsSerialBalance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_SER_1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SER_P1");

        string serialNo = $"SN-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // 1. Inbound receipt: 1 serialized item @ 50.00
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    UnitCost = 50m,
                    SerialNumber = serialNo
                }
            }
        });

        // 2. Issue this serial number
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    SerialNumber = serialNo
                }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(1m, dto.Quantity);
        Assert.Equal(50m, dto.UnitCost);
        Assert.Equal(serialNo, dto.SerialNumber);

        // Verify balance depleted to 0
        var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.SerialNumber == serialNo);
        Assert.NotNull(bal);
        Assert.Equal(0m, bal.QuantityOnHand);
        Assert.Equal(0m, bal.QuantityAvailable);
    }

    [Fact]
    public async Task PostStockIssue_MissingSerial_Rejection_BeforeAvailabilityCheck()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_MSER");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SER_MS");

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    SerialNumber = "" // Missing!
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Serial number is required", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_InvalidSerial_UntrackedStock_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INVSER");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "UNTRACKED_P");

        // Receipt untracked stock
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, UnitCost = 10m }
            }
        });

        // Issue with serial number on untracked product
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    SerialNumber = "SN-NOT-ALLOWED"
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("does not track serial numbers", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_SerialQuantityOtherThanOne_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_SERQTY");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SER_QTY");

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 2m, // Must be 1!
                    SerialNumber = "SN-12345"
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Serial-tracked items must have quantity of 1", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_DuplicateSerialInSameRequest_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_DUPSER");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SER_DUP");

        string serialNo = $"SN-DUP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Issue request has 2 lines both requesting the exact same serial number
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 1m, SerialNumber = serialNo },
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 1m, SerialNumber = serialNo }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Duplicate serial number", ex.Message);
    }

    #endregion

    #region Combined Tracking Tests (14 - 16)

    [Fact]
    public async Task PostStockIssue_CombinedTracking_MissingBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_BS_MB");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.BatchAndSerial, "BS_MB");

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    SerialNumber = "SN-COMB-1",
                    BatchNumber = "" // Missing!
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Batch number is required", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_CombinedTracking_MissingSerial_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_BS_MS");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.BatchAndSerial, "BS_MS");

        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    BatchNumber = "LOT-COMB-1",
                    SerialNumber = "" // Missing!
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("Serial number is required", ex.Message);
    }

    [Fact]
    public async Task PostStockIssue_CombinedTracking_ValidIssue_Successful()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_BS_OK");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.BatchAndSerial, "BS_OK");

        string batchNo = $"LOT-BS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string serialNo = $"SN-BS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // 1. Inbound
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    UnitCost = 100m,
                    BatchNumber = batchNo,
                    SerialNumber = serialNo
                }
            }
        });

        // 2. Issue
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1m,
                    BatchNumber = batchNo,
                    SerialNumber = serialNo
                }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(batchNo, dto.BatchNumber);
        Assert.Equal(serialNo, dto.SerialNumber);
        Assert.Equal(1m, dto.Quantity);
        Assert.Equal(100m, dto.UnitCost);

        // BalanceOnHand becomes 0
        var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.SerialNumber == serialNo);
        Assert.NotNull(bal);
        Assert.Equal(0m, bal.QuantityOnHand);
    }

    #endregion

    #region FIFO and Reconciliation Tests (17 - 22)

    [Fact]
    public async Task PostStockIssue_MultipleFifoLayers_WithDifferentCosts_DepletedInOrder()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_FIFO_DIFF");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "FIFO_DIFF");

        // Receipt Layer 1: 5 units @ $10.00
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, UnitCost = 10m }
            }
        });

        // Receipt Layer 2: 5 units @ $20.00
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 5m, UnitCost = 20m }
            }
        });

        // Issue 7 units: Consumes Layer 1 entirely (5 @ $10 = $50) + Layer 2 partially (2 @ $20 = $40) => Total $90
        // Effective unit cost: 90 / 7 = 12.8571
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 7m }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(7m, dto.Quantity);
        Assert.Equal(90m, dto.TotalCost);
        Assert.Equal(12.8571m, dto.UnitCost);

        // Verify remaining layers
        var layers = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .OrderBy(l => l.LayerDate)
            .ThenBy(l => l.CostLayerId)
            .ToListAsync();

        Assert.Equal(2, layers.Count);
        Assert.True(layers[0].IsExhausted);
        Assert.Equal(0m, layers[0].RemainingQuantity);

        Assert.False(layers[1].IsExhausted);
        Assert.Equal(3m, layers[1].RemainingQuantity);

        // Overall balance = 3
        var bal = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseLocationId == location.WarehouseLocationId);
        Assert.Equal(3m, bal?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_MultipleFifoLayers_WithIdenticalCosts_DepletedInOrder()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_FIFO_SAME");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "FIFO_SAME");

        // Receipt Layer 1: 10 @ $25
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 25m }
            }
        });

        // Receipt Layer 2: 10 @ $25
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 25m }
            }
        });

        // Issue 12 units
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 12m }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var dto = results[0];
        Assert.Equal(12m, dto.Quantity);
        Assert.Equal(25m, dto.UnitCost);
        Assert.Equal(300m, dto.TotalCost);

        var layers = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .OrderBy(l => l.LayerDate)
            .ThenBy(l => l.CostLayerId)
            .ToListAsync();

        Assert.Equal(2, layers.Count);
        Assert.Equal(0m, layers[0].RemainingQuantity);
        Assert.True(layers[0].IsExhausted);

        Assert.Equal(8m, layers[1].RemainingQuantity);
        Assert.False(layers[1].IsExhausted);
    }

    [Fact]
    public async Task PostStockIssue_IssueFromOneBatch_DoesNotConsumeAnotherBatchLayers()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ISO_BATCH");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "ISO_BATCH");

        string batchA = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batchB = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Inbound Batch A: 10 @ $15
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 15m, BatchNumber = batchA }
            }
        });

        // Inbound Batch B: 10 @ $25
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 25m, BatchNumber = batchB }
            }
        });

        // Issue 6 units from Batch A
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 6m, BatchNumber = batchA }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        Assert.Equal(15m, results[0].UnitCost);
        Assert.Equal(90m, results[0].TotalCost);

        // Verify Batch A: balance = 4, layer = 4
        var balA = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchA);
        Assert.Equal(4m, balA?.QuantityOnHand);

        // Verify Batch B: completely untouched! balance = 10, layer = 10 @ 25
        var balB = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchB);
        Assert.Equal(10m, balB?.QuantityOnHand);

        var layerB = await db.InventoryCostLayers.AsNoTracking().FirstOrDefaultAsync(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == batchB);
        Assert.Equal(10m, layerB?.RemainingQuantity);
        Assert.Equal(25m, layerB?.UnitCost);
    }

    [Fact]
    public async Task PostStockIssue_MultipleBatchesOfTheSameProduct_MultiLineVoucher()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_MLB");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "MLB_PRD");

        string batch1 = $"LOT-1-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batch2 = $"LOT-2-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Inbound: Batch 1 (10 @ $10), Batch 2 (10 @ $20)
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 10m, BatchNumber = batch1 },
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 20m, BatchNumber = batch2 }
            }
        });

        // Multi-line issue: Line 1 issues 3 from Batch 1, Line 2 issues 4 from Batch 2
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 3m, BatchNumber = batch1 },
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 4m, BatchNumber = batch2 }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Equal(2, results.Count);

        Assert.Equal(3m, results[0].Quantity);
        Assert.Equal(10m, results[0].UnitCost);
        Assert.Equal(batch1, results[0].BatchNumber);

        Assert.Equal(4m, results[1].Quantity);
        Assert.Equal(20m, results[1].UnitCost);
        Assert.Equal(batch2, results[1].BatchNumber);

        // Balances updated accordingly
        var bal1 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch1);
        Assert.Equal(7m, bal1?.QuantityOnHand);

        var bal2 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch2);
        Assert.Equal(6m, bal2?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_FifoAcceptanceScenario_ReconcilesLedgerAndCostLayers()
    {
        // Deterministic Acceptance Scenario:
        // Batch LOT-A Layer 1: 10 @ 20
        // Batch LOT-A Layer 2: 10 @ 25
        // Batch LOT-B Layer 3: 15 @ 30
        // Issue 14 units from LOT-A.
        // Expected:
        //   LOT-A Layer 1: 0 remaining
        //   LOT-A Layer 2: 6 remaining
        //   LOT-B Layer 3: 15 remaining
        //   Issue cost: 300
        //   Effective unit cost: 21.4286
        //   LOT-A balance: 6
        //   LOT-B balance: 15
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_ACCEPT_FIFO");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "ACCEPT_FIFO");

        string lotA = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string lotB = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // 1. Inbound Layer 1: LOT-A, Qty 10 @ 20
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 20m, BatchNumber = lotA }
            }
        });

        // 2. Inbound Layer 2: LOT-A, Qty 10 @ 25
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 25m, BatchNumber = lotA }
            }
        });

        // 3. Inbound Layer 3: LOT-B, Qty 15 @ 30
        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 15m, UnitCost = 30m, BatchNumber = lotB }
            }
        });

        // 4. Issue 14 units from LOT-A
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 14m, BatchNumber = lotA }
            }
        };

        var results = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results);
        var issueDto = results[0];

        // Issue cost verification: (10 * 20) + (4 * 25) = 200 + 100 = 300
        Assert.Equal(300m, issueDto.TotalCost);
        // Effective unit cost: 300 / 14 = 21.4286
        Assert.Equal(21.4286m, issueDto.UnitCost);
        Assert.Equal(14m, issueDto.Quantity);

        // Verify cost layers for LOT-A
        var layersA = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == lotA)
            .OrderBy(l => l.LayerDate)
            .ThenBy(l => l.CostLayerId)
            .ToListAsync();

        Assert.Equal(2, layersA.Count);
        // Layer 1: 0 remaining
        Assert.Equal(0m, layersA[0].RemainingQuantity);
        Assert.True(layersA[0].IsExhausted);

        // Layer 2: 6 remaining
        Assert.Equal(6m, layersA[1].RemainingQuantity);
        Assert.False(layersA[1].IsExhausted);

        // Verify cost layer for LOT-B: 15 remaining (untouched)
        var layersB = await db.InventoryCostLayers.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == lotB)
            .ToListAsync();

        Assert.Single(layersB);
        Assert.Equal(15m, layersB[0].RemainingQuantity);
        Assert.False(layersB[0].IsExhausted);

        // Verify LOT-A balance = 6
        var balA = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == lotA);
        Assert.Equal(6m, balA?.QuantityOnHand);

        // Verify LOT-B balance = 15
        var balB = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == lotB);
        Assert.Equal(15m, balB?.QuantityOnHand);

        // Verify InventoryTransaction in ledger
        var tx = await db.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.InventoryTransactionId == issueDto.StockIssueId);
        Assert.NotNull(tx);
        Assert.Equal(-14m, tx.Quantity);
        Assert.Equal(21.4286m, tx.UnitCost);
        Assert.Equal(300m, tx.TotalCost);
    }

    [Fact]
    public async Task PostStockIssue_DuplicateSubmission_DoesNotDeductStockTwice()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_IDEMP_B");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "IDEMP_B");

        string batchNo = $"LOT-IDEMP-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 15m, BatchNumber = batchNo }
            }
        });

        string sourceRef = $"DOC-REF-{Guid.NewGuid():N}";
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            SourceDocumentReference = sourceRef,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 4m, BatchNumber = batchNo }
            }
        };

        // 1st call succeeds
        var results1 = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(results1);

        var bal1 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.Equal(6m, bal1?.QuantityOnHand);

        // 2nd call with exact same reference fails
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockIssueAsync(tenantId, issueReq));
        Assert.Contains("already been posted", ex.Message);

        // Balance must remain at 6, not 2!
        var bal2 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.Equal(6m, bal2?.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockIssue_SourceDocumentReference_RetrievableInQueries()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REF_QRY");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "REF_QRY");

        await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 10m, UnitCost = 10m }
            }
        });

        string sourceRef = $"REF-QRY-{Guid.NewGuid():N}"[..24];
        var issueReq = new CreateStockIssueRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Purpose = StockIssuePurpose.InternalUse,
            SourceDocumentReference = sourceRef,
            Lines = new List<CreateStockIssueLineRequest>
            {
                new() { ProductId = product.ProductId, WarehouseLocationId = location.WarehouseLocationId, Quantity = 2m }
            }
        };

        var created = await inventoryService.CreateStockIssueAsync(tenantId, issueReq);
        Assert.Single(created);
        Guid issueId = created[0].StockIssueId;

        // Query by ID
        var singleDto = await inventoryService.GetStockIssueByIdAsync(tenantId, issueId);
        Assert.NotNull(singleDto);
        Assert.NotNull(singleDto.SourceDocumentReference);

        // Query list
        var list = await inventoryService.GetStockIssuesAsync(tenantId, warehouse.WarehouseId);
        var match = list.FirstOrDefault(i => i.StockIssueId == issueId);
        Assert.NotNull(match);
        Assert.NotNull(match.SourceDocumentReference);
    }

    #endregion

    #region Helper Methods

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", companyId: TestConstants.CompanyAId, branchId: TestConstants.BranchA1Id, city: "Colombo");
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId, string codePrefix)
    {
        string code = $"{codePrefix}_{Guid.NewGuid():N}"[..18];
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
        string code = $"P{Guid.NewGuid():N}"[..18];
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

    #endregion
}
