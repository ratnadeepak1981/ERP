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

public class InventoryTransferValuationIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public InventoryTransferValuationIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    #region Batch & Serial Tracking Tests (1 - 12)

    [Fact]
    public async Task Transfer_BatchTracked_FullBatch_Success()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_B1");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_B1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = DateTime.UtcNow.AddMonths(-1).Date;
        var expDate = DateTime.UtcNow.AddMonths(12).Date;

        // Inbound receipt: 10 units @ 25.00
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 25m,
            BatchNumber = batchNo,
            ManufacturingDate = mfgDate,
            ExpiryDate = expDate
        });

        // Full batch transfer: 10 units
        var transferDocId = Guid.NewGuid();
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = transferDocId,
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 10m,
            BatchNumber = batchNo
        });

        Assert.Equal(-10m, outTxn.Quantity);
        Assert.Equal(10m, inTxn.Quantity);
        Assert.Equal(25m, outTxn.UnitCost);
        Assert.Equal(25m, inTxn.UnitCost);
        Assert.Equal(batchNo, outTxn.BatchNumber);
        Assert.Equal(batchNo, inTxn.BatchNumber);

        // Verify source balance
        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId, batchNo);
        Assert.NotNull(srcBal);
        Assert.Equal(0m, srcBal.QuantityOnHand);
        Assert.Equal(0m, srcBal.QuantityAvailable);

        // Verify destination balance
        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, batchNo);
        Assert.NotNull(dstBal);
        Assert.Equal(10m, dstBal.QuantityOnHand);
        Assert.Equal(10m, dstBal.QuantityAvailable);
    }

    [Fact]
    public async Task Transfer_BatchTracked_PartialBatch_Success()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_B2");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_B2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 15m,
            BatchNumber = batchNo,
            ExpiryDate = DateTime.UtcNow.AddMonths(6).Date
        });

        // Partial transfer: 7 units
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 7m,
            BatchNumber = batchNo
        });

        Assert.Equal(-7m, outTxn.Quantity);
        Assert.Equal(7m, inTxn.Quantity);

        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId, batchNo);
        Assert.Equal(13m, srcBal!.QuantityOnHand);
        Assert.Equal(13m, srcBal.QuantityAvailable);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, batchNo);
        Assert.Equal(7m, dstBal!.QuantityOnHand);
        Assert.Equal(7m, dstBal.QuantityAvailable);
    }

    [Fact]
    public async Task Transfer_BatchTracked_MultipleBatches_Success()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_B3");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_B3");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batch1 = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batch2 = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Inbound Batch 1: 10 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m,
            BatchNumber = batch1,
            ExpiryDate = DateTime.UtcNow.AddMonths(12).Date
        });

        // Inbound Batch 2: 10 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 12m,
            BatchNumber = batch2,
            ExpiryDate = DateTime.UtcNow.AddMonths(12).Date
        });

        // Transfer 4 units from Batch 1
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 4m,
            BatchNumber = batch1
        });

        // Transfer 6 units from Batch 2
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 6m,
            BatchNumber = batch2
        });

        // Verify balances
        var srcBal1 = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId, batch1);
        Assert.Equal(6m, srcBal1!.QuantityOnHand);

        var srcBal2 = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId, batch2);
        Assert.Equal(4m, srcBal2!.QuantityOnHand);

        var dstBal1 = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, batch1);
        Assert.Equal(4m, dstBal1!.QuantityOnHand);

        var dstBal2 = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, batch2);
        Assert.Equal(6m, dstBal2!.QuantityOnHand);
    }

    [Fact]
    public async Task Transfer_BatchTracked_BatchIdentityPreserved_AtDestination()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_ID");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_ID");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var mfgDate = DateTime.UtcNow.AddMonths(-2).Date;
        var expDate = DateTime.UtcNow.AddMonths(10).Date;

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 50m,
            BatchNumber = batchNo,
            ManufacturingDate = mfgDate,
            ExpiryDate = expDate
        });

        int batchCountBefore = await db.ProductBatches.CountAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId);
        Assert.Equal(1, batchCountBefore);

        // Transfer
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 5m,
            BatchNumber = batchNo
        });

        // Crucial requirement: Never create a new ProductBatch during transfer
        int batchCountAfter = await db.ProductBatches.CountAsync(b => b.TenantId == tenantId && b.ProductId == product.ProductId);
        Assert.Equal(1, batchCountAfter);

        // Transaction records inherited dates
        Assert.Equal(expDate, inTxn.ExpiryDate);
        Assert.Equal(batchNo, inTxn.BatchNumber);

        // Destination cost layer retains batch
        var dstLayer = await db.InventoryCostLayers.FirstOrDefaultAsync(l =>
            l.TenantId == tenantId && l.InventoryTransactionId == inTxn.InventoryTransactionId);
        Assert.NotNull(dstLayer);
        Assert.Equal(batchNo, dstLayer.BatchNumber);
    }

    [Fact]
    public async Task Transfer_BatchTracked_InactiveBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_INACT");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_INACT");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m,
            BatchNumber = batchNo,
            ExpiryDate = DateTime.UtcNow.AddMonths(10).Date
        });

        // Deactivate batch
        var batch = await db.ProductBatches.FirstAsync(b => b.TenantId == tenantId && b.BatchNumber == batchNo);
        batch.Deactivate();
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 5m,
                BatchNumber = batchNo
            }));

        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_BatchTracked_ExpiredBatch_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_EXP");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_EXP");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        // Create an already-expired batch in DB directly or via initial inflow dated earlier
        var productBatch = ProductBatch.Create(
            tenantId,
            product.ProductId,
            batchNo,
            DateTime.UtcNow.AddYears(-2).Date,
            DateTime.UtcNow.AddDays(-1).Date);
        await db.ProductBatches.AddAsync(productBatch);

        // Balance exists
        var bal = InventoryBalance.Create(tenantId, wh.WarehouseId, srcLoc.WarehouseLocationId, product.ProductId, batchNo);
        bal.ApplyOnHandChange(10m);
        await db.InventoryBalances.AddAsync(bal);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 5m,
                BatchNumber = batchNo
            }));

        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_BatchTracked_WrongProductOrTenant_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_WRP");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_WRP");
        var prodA = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);
        var prodB = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchB = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = prodB.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m,
            BatchNumber = batchB,
            ExpiryDate = DateTime.UtcNow.AddMonths(6).Date
        });

        // Transfer ProdA using ProdB's batch
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = prodA.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 5m,
                BatchNumber = batchB
            }));

        Assert.Contains("another product", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_BatchTracked_InsufficientBatchQuantity_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_INS");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_INS");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 10m,
            BatchNumber = batchNo,
            ExpiryDate = DateTime.UtcNow.AddMonths(10).Date
        });

        // Request 6 units when only 5 exist
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 6m,
                BatchNumber = batchNo
            }));

        Assert.Contains("Insufficient available stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_SerialTracked_Success_IdentityPreserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_SER");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_SER");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, ValuationMethod.FIFO);

        string serialNo = $"SN-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        // Inbound 1 unit serial
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 1m,
            UnitCost = 150m,
            SerialNumber = serialNo
        });

        // Transfer serial
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 1m,
            SerialNumber = serialNo
        });

        Assert.Equal(-1m, outTxn.Quantity);
        Assert.Equal(1m, inTxn.Quantity);
        Assert.Equal(serialNo, inTxn.SerialNumber);

        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId, serialNumber: serialNo);
        Assert.Equal(0m, srcBal!.QuantityOnHand);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, serialNumber: serialNo);
        Assert.Equal(1m, dstBal!.QuantityOnHand);
    }

    [Fact]
    public async Task Transfer_SerialTracked_DuplicateSerialOrUnavailable_Rejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_S_DUP");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_S_DUP");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial, ValuationMethod.FIFO);

        string serialNo = $"SN-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        // Transfer non-existent serial
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 1m,
                SerialNumber = serialNo
            }));

        Assert.Contains("No stock exists", ex1.Message, StringComparison.OrdinalIgnoreCase);

        // Attempt transfer of serial with quantity > 1
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 2m,
                SerialNumber = serialNo
            }));

        Assert.Contains("must have quantity of 1", ex2.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_BatchAndSerial_Success_BothIdentitiesPreserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_BS");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_BS");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.BatchAndSerial, ValuationMethod.FIFO);

        string batchNo = $"LOT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string serialNo = $"SN-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 1m,
            UnitCost = 250m,
            BatchNumber = batchNo,
            SerialNumber = serialNo,
            ExpiryDate = DateTime.UtcNow.AddMonths(18).Date
        });

        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 1m,
            BatchNumber = batchNo,
            SerialNumber = serialNo
        });

        Assert.Equal(batchNo, inTxn.BatchNumber);
        Assert.Equal(serialNo, inTxn.SerialNumber);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId, batchNo, serialNo);
        Assert.Equal(1m, dstBal!.QuantityOnHand);
    }

    [Fact]
    public async Task Transfer_UntrackedStock_Success()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_UNTR");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_UNTR");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 5m
        });

        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 20m
        });

        Assert.Equal(-20m, outTxn.Quantity);
        Assert.Equal(20m, inTxn.Quantity);
        Assert.True(string.IsNullOrEmpty(outTxn.BatchNumber));
        Assert.True(string.IsNullOrEmpty(outTxn.SerialNumber));
        Assert.True(string.IsNullOrEmpty(inTxn.BatchNumber));
        Assert.True(string.IsNullOrEmpty(inTxn.SerialNumber));

        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(30m, srcBal!.QuantityOnHand);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(20m, dstBal!.QuantityOnHand);
    }

    #endregion

    #region FIFO Cost Valuation Tests (13 - 16)

    [Fact]
    public async Task Transfer_FIFO_FullTransfer_RelievesAllLayers()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_F1");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_F1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // Layer 1: 5 @ 10 = 50
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 10m
        });

        // Layer 2: 5 @ 20 = 100
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 5m,
            UnitCost = 20m
        });

        // Full transfer: 10 units
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 10m
        });

        // Total relieved: 5*10 + 5*20 = 150. Unit cost = 15.00
        Assert.Equal(15m, outTxn.UnitCost);
        Assert.Equal(15m, inTxn.UnitCost);
        Assert.Equal(150m, inTxn.TotalCost);

        // Check source layers exhausted
        var srcLayers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.WarehouseId == wh1.WarehouseId && l.ProductId == product.ProductId)
            .ToListAsync();
        Assert.All(srcLayers, l => Assert.True(l.IsExhausted));
        Assert.All(srcLayers, l => Assert.Equal(0m, l.RemainingQuantity));

        // Check destination layer created
        var dstLayers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.WarehouseId == wh2.WarehouseId && l.ProductId == product.ProductId)
            .ToListAsync();
        Assert.Single(dstLayers);
        Assert.Equal(10m, dstLayers[0].RemainingQuantity);
        Assert.Equal(15m, dstLayers[0].UnitCost);
        Assert.Equal(150m, dstLayers[0].RemainingCost);
    }

    [Fact]
    public async Task Transfer_FIFO_PartialTransferAcrossMultipleLayers_PreservesCosts()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_F2");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_F2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // Layer 1: 10 @ 10 = 100
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m
        });

        // Layer 2: 10 @ 20 = 200
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m
        });

        // Transfer 14 units across layers
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 14m
        });

        // Relieved: 10 from L1 ($100), 4 from L2 ($80) -> Total $180. Unit cost = 180 / 14 = 12.8571
        Assert.Equal(180m, inTxn.TotalCost);
        Assert.Equal(12.8571m, inTxn.UnitCost);

        // Source layers: L1 exhausted, L2 has 6 remaining @ $20 = $120
        var srcLayers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.WarehouseId == wh1.WarehouseId && l.ProductId == product.ProductId)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();

        Assert.Equal(0m, srcLayers[0].RemainingQuantity);
        Assert.True(srcLayers[0].IsExhausted);

        Assert.Equal(6m, srcLayers[1].RemainingQuantity);
        Assert.False(srcLayers[1].IsExhausted);
        Assert.Equal(120m, srcLayers[1].RemainingCost);

        // Total company value: 120 (source) + 180 (dest) = 300 (original)
        var dstLayer = await db.InventoryCostLayers.FirstAsync(l =>
            l.TenantId == tenantId && l.WarehouseId == wh2.WarehouseId && l.ProductId == product.ProductId);
        Assert.Equal(14m, dstLayer.RemainingQuantity);
        Assert.Equal(180m, dstLayer.RemainingCost);
    }

    [Fact]
    public async Task Transfer_FIFO_LayerReconciliation_TotalCompanyValueConserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var wh3 = await CreateTestWarehouseAsync(db, tenantId);
        var loc1 = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "LOC1");
        var loc2 = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "LOC2");
        var loc3 = await CreateTestLocationAsync(db, tenantId, wh3.WarehouseId, "LOC3");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // Receipts into WH1: 10 @ 20 ($200) + 10 @ 30 ($300) = $500
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
            UnitCost = 20m
        });
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = loc1.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 30m
        });

        // 1. Transfer 12 from WH1 to WH2
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = loc1.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = loc2.WarehouseLocationId,
            Quantity = 12m
        });

        // 2. Transfer 5 from WH2 to WH3
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh2.WarehouseId,
            SourceLocationId = loc2.WarehouseLocationId,
            DestinationWarehouseId = wh3.WarehouseId,
            DestinationLocationId = loc3.WarehouseLocationId,
            Quantity = 5m
        });

        // Verify total quantities and total valuation across all layers in all warehouses
        var allLayers = await db.InventoryCostLayers
            .Where(l => l.TenantId == tenantId && l.ProductId == product.ProductId)
            .ToListAsync();

        decimal totalRemainingQuantity = allLayers.Sum(l => l.RemainingQuantity);
        decimal totalRemainingCost = allLayers.Sum(l => l.RemainingCost);

        Assert.Equal(20m, totalRemainingQuantity);
        Assert.Equal(500m, totalRemainingCost);
    }

    [Fact]
    public async Task Transfer_FIFO_BatchSpecificTransfer_ConsumesOnlyTargetBatchLayers()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_FB");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_FB");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.Batch, ValuationMethod.FIFO);

        string batchA = $"LOT-A-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        string batchB = $"LOT-B-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Batch A Layer 1: 10 @ 10 = 100
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m,
            BatchNumber = batchA,
            ExpiryDate = DateTime.UtcNow.AddMonths(12).Date
        });

        // Batch A Layer 2: 10 @ 15 = 150
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 15m,
            BatchNumber = batchA,
            ExpiryDate = DateTime.UtcNow.AddMonths(12).Date
        });

        // Batch B Layer 3: 15 @ 25 = 375
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 3,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 15m,
            UnitCost = 25m,
            BatchNumber = batchB,
            ExpiryDate = DateTime.UtcNow.AddMonths(12).Date
        });

        // Transfer 14 units of Batch A
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 14m,
            BatchNumber = batchA
        });

        // Relieved: 10 from A1 ($100), 4 from A2 ($60) -> $160 total. Unit cost = 160 / 14 = 11.4286
        Assert.Equal(160m, inTxn.TotalCost);
        Assert.Equal(11.4286m, inTxn.UnitCost);

        // Verify Batch B layer is completely unaffected
        var layerB = await db.InventoryCostLayers.FirstAsync(l =>
            l.TenantId == tenantId && l.ProductId == product.ProductId && l.BatchNumber == batchB);
        Assert.Equal(15m, layerB.RemainingQuantity);
        Assert.Equal(375m, layerB.RemainingCost);
        Assert.False(layerB.IsExhausted);
    }

    #endregion

    #region Weighted Average Costing Tests (17 - 20)

    [Fact]
    public async Task Transfer_WeightedAverage_FullTransfer_AcrossWarehouses()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_WA1");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_WA1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.WeightedAverage);

        // Inbound 10 @ 20 ($200) + 10 @ 30 ($300) -> Total 20 units, value $500, avg $25
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 20m
        });
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 2,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 30m
        });

        // Full transfer 20 units
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 20m
        });

        Assert.Equal(25m, outTxn.UnitCost);
        Assert.Equal(25m, inTxn.UnitCost);
        Assert.Equal(500m, inTxn.TotalCost);

        var valBal1 = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh1.WarehouseId);
        Assert.Equal(0m, valBal1.TotalQuantity);
        Assert.Equal(0m, valBal1.TotalCostValue);

        var valBal2 = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh2.WarehouseId);
        Assert.Equal(20m, valBal2.TotalQuantity);
        Assert.Equal(500m, valBal2.TotalCostValue);
        Assert.Equal(25m, valBal2.CurrentAverageCost);
    }

    [Fact]
    public async Task Transfer_WeightedAverage_PartialTransfer_AcrossWarehouses()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_WA2");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_WA2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.WeightedAverage);

        // WH1: 20 units @ 25 = $500
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 25m
        });

        // WH2 already has: 10 units @ 40 = $400
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh2.WarehouseId,
            WarehouseLocationId = dstLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 40m
        });

        // Transfer 8 units from WH1 to WH2
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 8m
        });

        // Transferred value = 8 * 25 = 200
        Assert.Equal(25m, outTxn.UnitCost);
        Assert.Equal(25m, inTxn.UnitCost);
        Assert.Equal(200m, inTxn.TotalCost);

        // WH1 remaining: 12 units, value 300, avg 25
        var bal1 = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh1.WarehouseId);
        Assert.Equal(12m, bal1.TotalQuantity);
        Assert.Equal(300m, bal1.TotalCostValue);
        Assert.Equal(25m, bal1.CurrentAverageCost);

        // WH2 new: 10 + 8 = 18 units, value 400 + 200 = 600, avg = 600 / 18 = 33.3333
        var bal2 = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh2.WarehouseId);
        Assert.Equal(18m, bal2.TotalQuantity);
        Assert.Equal(600m, bal2.TotalCostValue);
        Assert.Equal(33.3333m, bal2.CurrentAverageCost);
    }

    [Fact]
    public async Task Transfer_WeightedAverage_Reconciliation_TotalCompanyValueConserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_WAR");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_WAR");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.WeightedAverage);

        // Receipts
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 100m,
            UnitCost = 17.50m
        });

        decimal companyValueBefore = await db.ProductValuationBalances
            .Where(b => b.TenantId == tenantId && b.ProductId == product.ProductId)
            .SumAsync(b => b.TotalCostValue);

        // Transfer 33 units
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 33m
        });

        decimal companyValueAfter = await db.ProductValuationBalances
            .Where(b => b.TenantId == tenantId && b.ProductId == product.ProductId)
            .SumAsync(b => b.TotalCostValue);

        Assert.Equal(companyValueBefore, companyValueAfter);
    }

    [Fact]
    public async Task Transfer_WeightedAverage_RepeatedTransfers_ZeroValuationDrift()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var locA = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_A");
        var locB = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_B");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.WeightedAverage);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = locA.WarehouseLocationId,
            Quantity = 50m,
            UnitCost = 23.45m
        });

        var initialBal = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh.WarehouseId);
        decimal initialAvg = initialBal.CurrentAverageCost;
        decimal initialVal = initialBal.TotalCostValue;

        // Repeated transfers back and forth inside the same warehouse
        for (int i = 0; i < 4; i++)
        {
            // LocA -> LocB
            await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = locA.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = locB.WarehouseLocationId,
                Quantity = 10m
            });

            // LocB -> LocA
            await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = locB.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = locA.WarehouseLocationId,
                Quantity = 10m
            });
        }

        var finalBal = await db.ProductValuationBalances.FirstAsync(b =>
            b.TenantId == tenantId && b.ProductId == product.ProductId && b.WarehouseId == wh.WarehouseId);

        Assert.Equal(initialAvg, finalBal.CurrentAverageCost);
        Assert.Equal(initialVal, finalBal.TotalCostValue);
        Assert.Equal(50m, finalBal.TotalQuantity);
    }

    #endregion

    #region Standard Costing Tests (21 - 24)

    [Fact]
    public async Task Transfer_StandardCost_FullTransfer_AtStandardCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_SC1");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_SC1");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.StandardCost, standardCost: 45m);

        // Receive 20 units
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 45m
        });

        // Full transfer: 20 units
        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 20m
        });

        Assert.Equal(45m, outTxn.UnitCost);
        Assert.Equal(45m, inTxn.UnitCost);
        Assert.Equal(900m, inTxn.TotalCost);
        Assert.Equal(900m, outTxn.TotalCost);
    }

    [Fact]
    public async Task Transfer_StandardCost_PartialTransfer_AtStandardCost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_SC2");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_SC2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.StandardCost, standardCost: 50m);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 50m
        });

        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 4m
        });

        Assert.Equal(50m, inTxn.UnitCost);
        Assert.Equal(200m, inTxn.TotalCost);

        var srcBal = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(6m, srcBal!.QuantityOnHand);

        var dstBal = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, product.ProductId);
        Assert.Equal(4m, dstBal!.QuantityOnHand);
    }

    [Fact]
    public async Task Transfer_StandardCost_Reconciliation_TotalCompanyValueConserved()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_SCR");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_SCR");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.StandardCost, standardCost: 75m);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 75m
        });

        var (outTxn, inTxn) = await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 3m
        });

        decimal sourceReduction = Math.Abs(outTxn.TotalCost);
        decimal destIncrease = inTxn.TotalCost;
        Assert.Equal(sourceReduction, destIncrease);
        Assert.Equal(225m, destIncrease);
    }

    [Fact]
    public async Task Transfer_StandardCost_NoStandardCostUpdateOrTransferVariance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "SRC_SCV");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "DST_SCV");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.StandardCost, standardCost: 80m);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh1.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 80m
        });

        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh1.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh2.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 5m
        });

        // Verify standard cost on product is NOT updated by transfer
        var freshProduct = await db.Products.AsNoTracking().FirstAsync(p => p.ProductId == product.ProductId);
        Assert.Equal(80m, freshProduct.StandardCost);
    }

    #endregion

    #region Safety, Concurrency, and Isolation Tests (25 - 29)

    [Fact]
    public async Task Transfer_MultiLine_AtomicRollback_OnFailure()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_ATM");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_ATM");
        var prodA = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);
        var prodB = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // Receive 10 units for Prod A, but only 2 units for Prod B
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = prodA.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
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
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 2m,
            UnitCost = 10m
        });

        // Wrap two transfer lines in an explicit ambient transaction
        var transferDocId = Guid.NewGuid();
        using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Line 1: Transfer 5 of Prod A (succeeds)
            await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = transferDocId,
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = prodA.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 5m
            });

            // Line 2: Transfer 10 of Prod B (fails: only 2 available!)
            await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = transferDocId,
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = prodB.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 10m
            });

            await tx.CommitAsync();
            Assert.Fail("Expected insufficient stock exception on line 2.");
        }
        catch (InvalidOperationException)
        {
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
        }

        // Verify Line 1 was rolled back
        var balA = await postingService.GetBalanceAsync(tenantId, srcLoc.WarehouseLocationId, prodA.ProductId);
        Assert.Equal(10m, balA!.QuantityOnHand);

        var dstBalA = await postingService.GetBalanceAsync(tenantId, dstLoc.WarehouseLocationId, prodA.ProductId);
        Assert.Null(dstBalA);
    }

    [Fact]
    public async Task Transfer_DuplicateSubmission_IdempotencyRejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_IDEM");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_IDEM");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 20m,
            UnitCost = 10m
        });

        var transferDocId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var req = new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = transferDocId,
            SourceDocumentLineId = lineId,
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 5m
        };

        // First transfer succeeds
        await postingService.PostTransferAsync(tenantId, req);

        // Duplicate transfer with identical SourceDocumentId and lineId
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, req));

        Assert.Contains("Duplicate transfer movement posting", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_Concurrency_CannotOverTransferAvailableStock()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var srcLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "SRC_CONC");
        var dstLoc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "DST_CONC");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // 10 units available
        await postingService.PostMovementAsync(tenantId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.GoodsReceipt,
            SourceDocumentType = SourceDocumentType.GoodsReceiptNote,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = product.ProductId,
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = srcLoc.WarehouseLocationId,
            Quantity = 10m,
            UnitCost = 10m
        });

        // First transfer of 7 units succeeds
        await postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
        {
            SourceDocumentType = SourceDocumentType.StockTransferOrder,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            ProductId = product.ProductId,
            SourceWarehouseId = wh.WarehouseId,
            SourceLocationId = srcLoc.WarehouseLocationId,
            DestinationWarehouseId = wh.WarehouseId,
            DestinationLocationId = dstLoc.WarehouseLocationId,
            Quantity = 7m
        });

        // Second transfer of 6 units fails (only 3 remaining)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh.WarehouseId,
                SourceLocationId = srcLoc.WarehouseLocationId,
                DestinationWarehouseId = wh.WarehouseId,
                DestinationLocationId = dstLoc.WarehouseLocationId,
                Quantity = 6m
            }));

        Assert.Contains("Insufficient available stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_LocationValidation_SameLocationAndCrossWarehouseRejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var wh1 = await CreateTestWarehouseAsync(db, tenantId);
        var wh2 = await CreateTestWarehouseAsync(db, tenantId);
        var loc1 = await CreateTestLocationAsync(db, tenantId, wh1.WarehouseId, "LOC_V1");
        var loc2 = await CreateTestLocationAsync(db, tenantId, wh2.WarehouseId, "LOC_V2");
        var product = await CreateTestProductAsync(db, tenantId, TrackingMode.None, ValuationMethod.FIFO);

        // Same location rejection
        var exSame = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh1.WarehouseId,
                SourceLocationId = loc1.WarehouseLocationId,
                DestinationWarehouseId = wh1.WarehouseId,
                DestinationLocationId = loc1.WarehouseLocationId,
                Quantity = 5m
            }));
        Assert.Contains("cannot be identical", exSame.Message, StringComparison.OrdinalIgnoreCase);

        // Cross warehouse location mismatch (loc1 belongs to wh1, but request specifies wh2)
        var exMismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostTransferAsync(tenantId, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = product.ProductId,
                SourceWarehouseId = wh2.WarehouseId, // Wrong warehouse for loc1
                SourceLocationId = loc1.WarehouseLocationId,
                DestinationWarehouseId = wh2.WarehouseId,
                DestinationLocationId = loc2.WarehouseLocationId,
                Quantity = 5m
            }));
        Assert.Contains("does not belong to warehouse", exMismatch.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transfer_TenantIsolation_CrossTenantLocationOrProductRejection()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var postingService = sp.GetRequiredService<IInventoryPostingService>();
        var db = sp.GetRequiredService<DomainDbContext>();

        var tenantA = TestConstants.TenantAlphaId;
        var tenantB = TestConstants.TenantBetaId;

        var whA = await CreateTestWarehouseAsync(db, tenantA);
        var locA = await CreateTestLocationAsync(db, tenantA, whA.WarehouseId, "LOC_TA");
        var productA = await CreateTestProductAsync(db, tenantA, TrackingMode.None, ValuationMethod.FIFO);

        var whB = await CreateTestWarehouseAsync(db, tenantB);
        var locB = await CreateTestLocationAsync(db, tenantB, whB.WarehouseId, "LOC_TB");

        // Attempt transfer in Tenant A with destination location from Tenant B
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            postingService.PostTransferAsync(tenantA, new PostTransferMovementRequest
            {
                SourceDocumentType = SourceDocumentType.StockTransferOrder,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                ProductId = productA.ProductId,
                SourceWarehouseId = whA.WarehouseId,
                SourceLocationId = locA.WarehouseLocationId,
                DestinationWarehouseId = whB.WarehouseId,
                DestinationLocationId = locB.WarehouseLocationId,
                Quantity = 5m
            }));
    }

    #endregion

    #region Helper Methods

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18];
        var companyId = tenantId == TestConstants.TenantBetaId ? TestConstants.CompanyCId : TestConstants.CompanyAId;
        var branchId = tenantId == TestConstants.TenantBetaId ? (Guid?)null : TestConstants.BranchA1Id;
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", companyId: companyId, branchId: branchId, city: "Colombo");
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

    private static async Task<Product> CreateTestProductAsync(
        DomainDbContext db,
        Guid tenantId,
        TrackingMode trackingMode,
        ValuationMethod valuationMethod = ValuationMethod.FIFO,
        decimal standardCost = 0)
    {
        string code = $"P{Guid.NewGuid():N}"[..18];
        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            TestConstants.CategoryAlphaId,
            unitOfMeasure: "PCS",
            trackingMode: trackingMode,
            valuationMethod: valuationMethod,
            standardCost: standardCost);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }

    #endregion
}
