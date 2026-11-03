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

public class StockReceiptBatchIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public StockReceiptBatchIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task PostStockReceipt_NewBatchWithDates_CreatesProductBatchAndCostLayer()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_PROD");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 8, 31, 0, 0, 0, DateTimeKind.Utc);

        var request = new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Remarks = "Initial batch receipt",
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 100m,
                    UnitCost = 12.50m,
                    BatchNumber = batchNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate,
                    Remarks = "Line 1 note"
                }
            }
        };

        var result = await inventoryService.CreateStockReceiptAsync(tenantId, request);

        Assert.NotNull(result);
        Assert.Single(result);
        var dto = result[0];

        Assert.Equal(StockReceiptStatus.Posted, dto.Status);
        Assert.Equal("Posted", dto.StatusName);
        Assert.Equal(100m, dto.Quantity);
        Assert.Equal(12.50m, dto.UnitCost);
        Assert.Equal(1250m, dto.TotalCost);
        Assert.Equal(batchNo, dto.BatchNumber);
        Assert.Equal(mfgDate.Date, dto.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, dto.ExpiryDate?.Date);

        // Verify ProductBatch master record
        var batch = await db.ProductBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(batch);
        Assert.True(batch.IsActive);
        Assert.Equal(mfgDate.Date, batch.ManufacturingDate);
        Assert.Equal(expDate.Date, batch.ExpiryDate);

        // Verify InventoryTransaction record
        var tx = await db.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.InventoryTransactionId == dto.StockReceiptId);
        Assert.NotNull(tx);
        Assert.Equal(100m, tx.Quantity);
        Assert.Equal(batchNo, tx.BatchNumber);
        Assert.Equal(expDate.Date, tx.ExpiryDate?.Date);

        // Verify InventoryBalance
        var balance = await db.InventoryBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == location.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(balance);
        Assert.Equal(100m, balance.QuantityOnHand);
        Assert.Equal(100m, balance.QuantityAvailable);

        // Verify Cost Layer
        var costLayer = await db.InventoryCostLayers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ProductId == product.ProductId && c.BatchNumber == batchNo);
        Assert.NotNull(costLayer);
        Assert.Equal(100m, costLayer.OriginalQuantity);
        Assert.Equal(100m, costLayer.RemainingQuantity);
        Assert.Equal(12.50m, costLayer.UnitCost);
        Assert.Equal(batchNo, costLayer.BatchNumber);
    }

    [Fact]
    public async Task PostStockReceipt_ReuseExistingBatchWithMatchingDates_SucceedsAndIncrementsBalance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_REUSE");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 9, 30, 0, 0, 0, DateTimeKind.Utc);

        // First receipt
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
                    Quantity = 40m,
                    UnitCost = 20m,
                    BatchNumber = batchNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate
                }
            }
        });

        // Second receipt for same batch with identical dates
        var result2 = await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 60m,
                    UnitCost = 22m,
                    BatchNumber = batchNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate
                }
            }
        });

        Assert.Single(result2);
        Assert.Equal(60m, result2[0].Quantity);

        // Verify balance accumulated to 100
        var balance = await db.InventoryBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == location.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(balance);
        Assert.Equal(100m, balance.QuantityOnHand);
        Assert.Equal(100m, balance.QuantityAvailable);

        // Verify 2 cost layers exist
        var costLayers = await db.InventoryCostLayers.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ProductId == product.ProductId && c.BatchNumber == batchNo)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
        Assert.Equal(2, costLayers.Count);
        Assert.Equal(40m, costLayers[0].RemainingQuantity);
        Assert.Equal(20m, costLayers[0].UnitCost);
        Assert.Equal(60m, costLayers[1].RemainingQuantity);
        Assert.Equal(22m, costLayers[1].UnitCost);
    }

    [Fact]
    public async Task PostStockReceipt_ReuseExistingBatchWithOmittedDates_InheritsAuthoritativeDates()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_3");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_INHERIT");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        // Pre-create batch master record with authoritative dates
        var batch = ProductBatch.Create(tenantId, product.ProductId, batchNo, mfgDate, expDate);
        await db.ProductBatches.AddAsync(batch);
        await db.SaveChangesAsync();

        // Stock receipt omitting both ManufacturingDate and ExpiryDate
        var result = await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 25m,
                    UnitCost = 15m,
                    BatchNumber = batchNo,
                    ManufacturingDate = null,
                    ExpiryDate = null
                }
            }
        });

        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(batchNo, dto.BatchNumber);
        Assert.Equal(mfgDate.Date, dto.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, dto.ExpiryDate?.Date);

        // Verify ledger transaction inherited the dates
        var tx = await db.InventoryTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.InventoryTransactionId == dto.StockReceiptId);
        Assert.NotNull(tx);
        Assert.Equal(expDate.Date, tx.ExpiryDate?.Date);
    }

    [Fact]
    public async Task PostStockReceipt_ConflictingExpiryDate_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_4");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_CONFLICT_EXP");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        var batch = ProductBatch.Create(tenantId, product.ProductId, batchNo, mfgDate, expDate);
        await db.ProductBatches.AddAsync(batch);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
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
                        BatchNumber = batchNo,
                        ManufacturingDate = mfgDate,
                        ExpiryDate = new DateTime(2027, 6, 20, 0, 0, 0, DateTimeKind.Utc) // Conflict!
                    }
                }
            }));

        Assert.Contains("Conflicting expiry date", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_ConflictingManufacturingDate_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_5");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_CONFLICT_MFG");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 5, 20, 0, 0, 0, DateTimeKind.Utc);

        var batch = ProductBatch.Create(tenantId, product.ProductId, batchNo, mfgDate, expDate);
        await db.ProductBatches.AddAsync(batch);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
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
                        BatchNumber = batchNo,
                        ManufacturingDate = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), // Conflict!
                        ExpiryDate = expDate
                    }
                }
            }));

        Assert.Contains("Conflicting manufacturing date", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_ExpiryEarlierThanManufacturing_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_6");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_INVALID_DATES");

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
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
                        BatchNumber = batchNo,
                        ManufacturingDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        ExpiryDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) // Earlier!
                    }
                }
            }));

        Assert.Contains("cannot be earlier than manufacturing date", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_BatchTrackedProduct_MissingBatchNumber_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_7");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "BATCH_REQUIRED");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
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
                        BatchNumber = null // Missing!
                    }
                }
            }));

        Assert.Contains("Batch number is required for batch-tracked product", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_NonBatchProduct_SucceedsWithoutBatchOrSerial()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_8");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "NON_BATCH");

        var result = await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 50m,
                    UnitCost = 10m,
                    BatchNumber = null,
                    SerialNumber = null
                }
            }
        });

        Assert.Single(result);
        Assert.Equal(50m, result[0].Quantity);
        Assert.Null(result[0].BatchNumber);
        Assert.Null(result[0].SerialNumber);
    }

    [Fact]
    public async Task PostStockReceipt_SerialTrackedProduct_MissingSerialNumber_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_9");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SERIAL_REQUIRED");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
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
                        SerialNumber = null // Missing!
                    }
                }
            }));

        Assert.Contains("Serial number is required for serial-tracked product", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_SerialTrackedProduct_QuantityNotOne_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_10");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, "SERIAL_QTY_1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
            {
                WarehouseId = warehouse.WarehouseId,
                Reason = StockReceiptReason.OpeningBalance,
                Lines = new List<CreateStockReceiptLineRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        WarehouseLocationId = location.WarehouseLocationId,
                        Quantity = 2m, // Invalid for serial!
                        SerialNumber = "SN-9999"
                    }
                }
            }));

        Assert.Contains("Serial-tracked items must have quantity of 1", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_MultiLine_MultipleBatches_Succeeds()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_11");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "MULTI_BATCH");

        string batch1 = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batch2 = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var mfgDate1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expDate1 = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var mfgDate2 = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var expDate2 = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 30m,
                    UnitCost = 10m,
                    BatchNumber = batch1,
                    ManufacturingDate = mfgDate1,
                    ExpiryDate = expDate1
                },
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 45m,
                    UnitCost = 11m,
                    BatchNumber = batch2,
                    ManufacturingDate = mfgDate2,
                    ExpiryDate = expDate2
                }
            }
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(batch1, result[0].BatchNumber);
        Assert.Equal(batch2, result[1].BatchNumber);

        // Verify both batches exist in ProductBatches
        var b1 = await db.ProductBatches.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch1);
        var b2 = await db.ProductBatches.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batch2);

        Assert.NotNull(b1);
        Assert.NotNull(b2);
        Assert.Equal(expDate1.Date, b1.ExpiryDate);
        Assert.Equal(expDate2.Date, b2.ExpiryDate);

        // Verify individual balances
        var bal1 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == location.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batch1);
        var bal2 = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == location.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batch2);

        Assert.NotNull(bal1);
        Assert.NotNull(bal2);
        Assert.Equal(30m, bal1.QuantityOnHand);
        Assert.Equal(45m, bal2.QuantityOnHand);
    }

    [Fact]
    public async Task PostStockReceipt_MultiLine_AtomicRollbackOnFailure()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_12");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "ATOMIC_ROLLBACK");

        string batchValid = $"LOT-OK-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batchInvalid = $"LOT-BAD-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Line 2 has invalid dates (expiry < manufacturing)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
            {
                WarehouseId = warehouse.WarehouseId,
                Reason = StockReceiptReason.OpeningBalance,
                Lines = new List<CreateStockReceiptLineRequest>
                {
                    new()
                    {
                        ProductId = product.ProductId,
                        WarehouseLocationId = location.WarehouseLocationId,
                        Quantity = 50m,
                        BatchNumber = batchValid,
                        ManufacturingDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        ExpiryDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new()
                    {
                        ProductId = product.ProductId,
                        WarehouseLocationId = location.WarehouseLocationId,
                        Quantity = 20m,
                        BatchNumber = batchInvalid,
                        ManufacturingDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        ExpiryDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) // Invalid!
                    }
                }
            }));

        // Assert that line 1 was NOT committed
        var batchOk = await db.ProductBatches.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchValid);
        Assert.Null(batchOk);

        var balance = await db.InventoryBalances.AsNoTracking().FirstOrDefaultAsync(b => b.TenantId == tenantId && b.WarehouseLocationId == location.WarehouseLocationId && b.ProductId == product.ProductId && b.BatchNumber == batchValid);
        Assert.Null(balance);
    }

    [Fact]
    public async Task PostStockReceipt_DuplicateReceiptId_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_13");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "DUP_ID");

        Guid fixedReceiptId = Guid.NewGuid();

        var request = new CreateStockReceiptRequest
        {
            StockReceiptId = fixedReceiptId,
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 10m
                }
            }
        };

        // First attempt succeeds
        var res1 = await inventoryService.CreateStockReceiptAsync(tenantId, request);
        Assert.Single(res1);

        // Second attempt with exact same StockReceiptId fails idempotency check
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, request));

        Assert.Contains("has already been posted", ex.Message);
    }

    [Fact]
    public async Task PostStockReceipt_DuplicateSourceDocumentReference_ThrowsInvalidOperationException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_14");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, "DUP_REF");

        string uniqueRef = $"PO-STOCK-REC-{Guid.NewGuid():N}";

        var request = new CreateStockReceiptRequest
        {
            SourceDocumentReference = uniqueRef,
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 15m
                }
            }
        };

        // First attempt succeeds
        await inventoryService.CreateStockReceiptAsync(tenantId, request);

        // Second attempt with exact same SourceDocumentReference fails
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.CreateStockReceiptAsync(tenantId, request));

        Assert.Contains("has already been posted", ex.Message);
    }

    [Fact]
    public async Task GetStockReceipts_And_GetStockReceiptById_ReturnsBatchAndDates()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_REC_15");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, "QUERY_BATCH");

        string batchNo = $"LOT-QRY-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 10, 31, 0, 0, 0, DateTimeKind.Utc);

        var created = await inventoryService.CreateStockReceiptAsync(tenantId, new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 75m,
                    UnitCost = 14m,
                    BatchNumber = batchNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate
                }
            }
        });

        Guid receiptId = created[0].StockReceiptId;

        // Query by Id
        var singleDto = await inventoryService.GetStockReceiptByIdAsync(tenantId, receiptId);
        Assert.NotNull(singleDto);
        Assert.Equal(receiptId, singleDto.StockReceiptId);
        Assert.Equal(batchNo, singleDto.BatchNumber);
        Assert.Equal(mfgDate.Date, singleDto.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, singleDto.ExpiryDate?.Date);

        // Query list
        var list = await inventoryService.GetStockReceiptsAsync(tenantId, warehouse.WarehouseId);
        var match = list.FirstOrDefault(r => r.StockReceiptId == receiptId);
        Assert.NotNull(match);
        Assert.Equal(batchNo, match.BatchNumber);
        Assert.Equal(mfgDate.Date, match.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, match.ExpiryDate?.Date);
    }

    [Fact]
    public async Task PostStockReceipt_CombinedTrackingMode_BatchAndSerial_EnforcesBothIdentifiersAndPostsCorrectly()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var inventoryService = sp.GetRequiredService<IInventoryService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_BS_1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.BatchAndSerial, "BS_PROD");

        string batchNo = $"LOT-BS-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string serialNo = $"SN-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var expDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        // 1. Missing BatchNumber throws InvalidOperationException
        var missingBatchReq = new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1,
                    UnitCost = 150m,
                    SerialNumber = serialNo
                }
            }
        };
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockReceiptAsync(tenantId, missingBatchReq));
        Assert.Contains("Batch number is required", ex1.Message);

        // 2. Missing SerialNumber throws InvalidOperationException
        var missingSerialReq = new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1,
                    UnitCost = 150m,
                    BatchNumber = batchNo
                }
            }
        };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockReceiptAsync(tenantId, missingSerialReq));
        Assert.Contains("Serial number is required", ex2.Message);

        // 3. Quantity != 1 throws InvalidOperationException
        var invalidQtyReq = new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 2,
                    UnitCost = 150m,
                    BatchNumber = batchNo,
                    SerialNumber = serialNo
                }
            }
        };
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => inventoryService.CreateStockReceiptAsync(tenantId, invalidQtyReq));
        Assert.Contains("Serial-tracked items must have quantity of 1", ex3.Message);

        // 4. Valid BatchAndSerial receipt with Qty = 1 succeeds
        var validReq = new CreateStockReceiptRequest
        {
            WarehouseId = warehouse.WarehouseId,
            Reason = StockReceiptReason.OpeningBalance,
            Remarks = "Valid combined batch and serial receipt",
            Lines = new List<CreateStockReceiptLineRequest>
            {
                new()
                {
                    ProductId = product.ProductId,
                    WarehouseLocationId = location.WarehouseLocationId,
                    Quantity = 1,
                    UnitCost = 150m,
                    BatchNumber = batchNo,
                    SerialNumber = serialNo,
                    ManufacturingDate = mfgDate,
                    ExpiryDate = expDate
                }
            }
        };
        var result = await inventoryService.CreateStockReceiptAsync(tenantId, validReq);
        Assert.NotEmpty(result);
        var receipt = result[0];
        Assert.Equal(batchNo, receipt.BatchNumber);
        Assert.Equal(mfgDate.Date, receipt.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, receipt.ExpiryDate?.Date);

        // Verify ProductBatch created
        var registeredBatch = await db.ProductBatches.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.BatchNumber == batchNo);
        Assert.NotNull(registeredBatch);
        Assert.Equal(mfgDate.Date, registeredBatch.ManufacturingDate?.Date);
        Assert.Equal(expDate.Date, registeredBatch.ExpiryDate?.Date);

        // Verify InventoryTransaction recorded both BatchNumber and SerialNumber
        var tx = await db.InventoryTransactions.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ProductId == product.ProductId && t.SerialNumber == serialNo);
        Assert.NotNull(tx);
        Assert.Equal(batchNo, tx.BatchNumber);
        Assert.Equal(serialNo, tx.SerialNumber);
        Assert.Equal(1, tx.Quantity);

        // Verify InventoryBalance recorded both
        var balance = await db.InventoryBalances.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId && b.SerialNumber == serialNo);
        Assert.NotNull(balance);
        Assert.Equal(batchNo, balance.BatchNumber);
        Assert.Equal(serialNo, balance.SerialNumber);
        Assert.Equal(1, balance.QuantityOnHand);
    }

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
}
