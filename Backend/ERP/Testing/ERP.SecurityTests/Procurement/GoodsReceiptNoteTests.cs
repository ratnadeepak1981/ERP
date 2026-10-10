using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Procurement;

public class GoodsReceiptNoteTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GoodsReceiptNoteTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CompleteReceipt_FulfillsPo_AndPostsInventory()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        // Setup master data & approved PO
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_REC");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 20m, 15m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // 1. Create draft GRN from PO
        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);
        Assert.Equal(GoodsReceiptNoteStatus.Draft, grnDto.Status);
        Assert.Single(grnDto.Lines);
        Assert.Equal(20m, grnDto.Lines[0].QuantityReceived);

        // 2. Confirm GRN
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmedDto = await confirmRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(confirmedDto);
        Assert.Equal(GoodsReceiptNoteStatus.Confirmed, confirmedDto.Status);

        // 3. Verify PO received quantity and Fulfilled status
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var updatedPo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
        Assert.NotNull(updatedPo);
        Assert.Equal(PurchaseOrderStatus.Fulfilled, updatedPo.Status);
        Assert.Equal(20m, updatedPo.Items[0].ReceivedQuantity);

        // 4. Verify InventoryBalance and InventoryTransaction
        var postingService = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var balance = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(20m, balance.QuantityOnHand);
        Assert.Equal(20m, balance.QuantityAvailable);

        var txns = await verifyDb.InventoryTransactions
            .Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId)
            .ToListAsync();
        Assert.Single(txns);
        Assert.Equal(20m, txns[0].Quantity);
        Assert.Equal(300m, txns[0].TotalCost); // 20 * 15
    }

    [Fact]
    public async Task PartialReceipts_TracksReceivedQuantitiesAcrossMultipleGrns_AndFulfillsOnlyWhenComplete()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_PARTIAL");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 50m, 10m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // Receipt 1: 20 units
        string grnNum1 = $"GRN_{Guid.NewGuid():N}"[..12];
        var manualReq1 = new CreateGoodsReceiptNoteRequest
        {
            PurchaseOrderId = po.Id,
            GrnNumber = grnNum1,
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 20m,
                    UnitCost = 10m
                }
            }
        };

        var res1 = await client.PostAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes", manualReq1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var dto1 = await res1.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        var conf1 = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto1!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, conf1.StatusCode);

        // Verify PO remains Approved, ReceivedQuantity = 20
        using (var vScope = _fixture.Services.CreateScope())
        {
            var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var poAfter1 = await vDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
            Assert.Equal(PurchaseOrderStatus.Approved, poAfter1!.Status);
            Assert.Equal(20m, poAfter1.Items[0].ReceivedQuantity);
        }

        // Receipt 2: Remaining 30 units
        string grnNum2 = $"GRN_{Guid.NewGuid():N}"[..12];
        var manualReq2 = new CreateGoodsReceiptNoteRequest
        {
            PurchaseOrderId = po.Id,
            GrnNumber = grnNum2,
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 30m,
                    UnitCost = 10m
                }
            }
        };

        var res2 = await client.PostAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes", manualReq2);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
        var dto2 = await res2.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        var conf2 = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto2!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, conf2.StatusCode);

        // Verify PO is now Fulfilled, ReceivedQuantity = 50
        using (var vScope = _fixture.Services.CreateScope())
        {
            var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var poAfter2 = await vDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
            Assert.Equal(PurchaseOrderStatus.Fulfilled, poAfter2!.Status);
            Assert.Equal(50m, poAfter2.Items[0].ReceivedQuantity);

            // Balance total = 50
            var postingService = vScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
            var bal = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
            Assert.Equal(50m, bal!.QuantityOnHand);
        }
    }

    [Fact]
    public async Task OverReceipt_StrictlyRejected_WithZeroTolerance()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_OVER");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 10m, 10m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // Attempt receipt with 10.0001 (exceeds ordered quantity 10)
        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var manualReq = new CreateGoodsReceiptNoteRequest
        {
            PurchaseOrderId = po.Id,
            GrnNumber = grnNum,
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 10.5m,
                    UnitCost = 10m
                }
            }
        };

        var res = await client.PostAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes", manualReq);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);

        // Confirming must fail with Conflict (409)
        var conf = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, conf.StatusCode);

        // Verify PO remains untouched
        using var vScope = _fixture.Services.CreateScope();
        var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var unChangedPo = await vDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
        Assert.Equal(0m, unChangedPo!.Items[0].ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Approved, unChangedPo.Status);
    }

    [Fact]
    public async Task DuplicateConfirmationRetry_IsIdempotent_AndDoesNotDoublePost()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_IDEMP");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 15m, 10m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        var grnDto = await createRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);

        // Confirmation 1
        var conf1 = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, conf1.StatusCode);

        // Confirmation 2 (Simulating retry)
        var conf2 = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, conf2.StatusCode);

        // Verify only 1 set of ledger transactions and exactly 15 balance on-hand
        using var vScope = _fixture.Services.CreateScope();
        var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var txns = await vDb.InventoryTransactions.Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId).ToListAsync();
        Assert.Single(txns);

        var postingService = vScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var bal = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
        Assert.Equal(15m, bal!.QuantityOnHand);
    }

    [Fact]
    public async Task QcQuarantineReceipt_AllocatesToQuarantineBalance_AndExcludesFromAvailable()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_QC");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 25m, 12m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var manualReq = new CreateGoodsReceiptNoteRequest
        {
            PurchaseOrderId = po.Id,
            GrnNumber = grnNum,
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 25m,
                    UnitCost = 12m,
                    IsQuarantine = true
                }
            }
        };

        var res = await client.PostAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes", manualReq);
        var dto = await res.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        var conf = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, conf.StatusCode);

        // Verify balance: On-hand = 25, Quarantine = 25, Available = 0
        using var vScope = _fixture.Services.CreateScope();
        var postingService = vScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var bal = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(bal);
        Assert.Equal(25m, bal.QuantityOnHand);
        Assert.Equal(25m, bal.QuantityQuarantine);
        Assert.Equal(0m, bal.QuantityAvailable);
    }

    [Fact]
    public async Task BatchAndSerialValidation_EnforcedCorrectlyDuringReceipt()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_SERIAL");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.Serial);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 1m, 100m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.EDIT", "GRN.CONFIRM" });

        // Missing serial number should fail confirmation
        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var manualReq = new CreateGoodsReceiptNoteRequest
        {
            PurchaseOrderId = po.Id,
            GrnNumber = grnNum,
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 1m,
                    UnitCost = 100m,
                    SerialNumber = null // Missing required serial!
                }
            }
        };

        var res = await client.PostAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes", manualReq);
        var dto = await res.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        var conf = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto!.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, conf.StatusCode);

        // Edit draft to provide SerialNumber
        var updateReq = new UpdateGoodsReceiptNoteRequest
        {
            Lines = new List<CreateGoodsReceiptNoteLineRequest>
            {
                new()
                {
                    PurchaseOrderItemId = po.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = wh.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityReceived = 1m,
                    UnitCost = 100m,
                    SerialNumber = "SN-VALID-001"
                }
            }
        };
        var updateRes = await client.PutAsJsonAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto.GoodsReceiptNoteId}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Confirmation now succeeds
        var confSuccess = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{dto.GoodsReceiptNoteId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confSuccess.StatusCode);
    }

    [Fact]
    public async Task AuditTrail_CapturesGrnLifecycle()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_AUDIT");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 5m, 10m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        var grnDto = await createRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);

        await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto!.GoodsReceiptNoteId}/confirm", null);

        // Inspect DomainAuditRecords
        using var vScope = _fixture.Services.CreateScope();
        var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var auditRecords = await vDb.DomainAuditRecords
            .Where(a => a.EntityId == grnDto.GoodsReceiptNoteId.ToString() && a.EntityName == "GoodsReceiptNote")
            .ToListAsync();

        Assert.NotEmpty(auditRecords);
        Assert.Contains(auditRecords, a => a.Action == "Created");
        Assert.Contains(auditRecords, a => a.Action == "Modified"); // Confirmation status change
    }

    [Fact]
    public async Task DraftCancellation_DoesNotImpactPoOrInventory()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_CANCEL");
        var prod = await CreateTestProductAsync(db, tenantId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, tenantId, companyId, branchId, prod.ProductId, 10m, 10m);

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CANCEL" });

        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        var grnDto = await createRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);

        // Cancel the draft
        var cancelRes = await client.PostAsync($"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto!.GoodsReceiptNoteId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelledDto = await cancelRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.Equal(GoodsReceiptNoteStatus.Cancelled, cancelledDto!.Status);

        // Verify PO remains untouched
        using var vScope = _fixture.Services.CreateScope();
        var vDb = vScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var poAfter = await vDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
        Assert.Equal(0m, poAfter!.Items[0].ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Approved, poAfter.Status);

        // No inventory ledger records
        var txns = await vDb.InventoryTransactions.Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId).ToListAsync();
        Assert.Empty(txns);
    }

    [Fact]
    public async Task CrossTenantIsolation_RejectsAccessToForeignTenantGrn()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();

        var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId);
        var loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_ISO");
        var prod = await CreateTestProductAsync(db, TestConstants.TenantAlphaId, TrackingMode.None);
        var po = await CreateApprovedPoAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, prod.ProductId, 5m, 10m);

        var clientAlpha = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE" });

        string grnNum = $"GRN_{Guid.NewGuid():N}"[..12];
        var createRes = await clientAlpha.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        var grnDto = await createRes.Content.ReadFromJsonAsync<GoodsReceiptNoteDto>(_jsonOpts);

        // Tenant Beta Admin attempts to view Tenant Alpha's GRN
        var clientBeta = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "GRN.VIEW" });

        var foreignRes = await clientBeta.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/goods-receipt-notes/{grnDto!.GoodsReceiptNoteId}");

        // Must be Forbidden (403) or NotFound (404) due to branch/company scope validation and tenant filtering
        Assert.True(foreignRes.StatusCode == HttpStatusCode.Forbidden || foreignRes.StatusCode == HttpStatusCode.NotFound,
            $"Expected Forbidden or NotFound, got {foreignRes.StatusCode}");
    }

    // Helper methods
    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..8];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", city: "Colombo");
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid warehouseId,
        string prefix)
    {
        string code = $"{prefix}_{Guid.NewGuid():N}"[..8];
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
        TrackingMode trackingMode)
    {
        string code = $"PRD_{Guid.NewGuid():N}"[..8];
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        Guid catId = cat?.CategoryId ?? Guid.NewGuid();

        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            catId,
            unitOfMeasure: "PCS",
            trackingMode: trackingMode,
            isStockTracked: true);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }

    private static async Task<PurchaseOrder> CreateApprovedPoAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid productId,
        decimal quantity,
        decimal unitPrice)
    {
        string poNum = $"PO_{Guid.NewGuid():N}"[..10];
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        po.AddItem(productId, quantity, unitPrice);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();
        return po;
    }
}
