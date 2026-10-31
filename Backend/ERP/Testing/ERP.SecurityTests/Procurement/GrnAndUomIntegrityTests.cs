using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Infrastructure.Persistence;
using Domain.Features.Inventory.Putaway;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Procurement;

[Collection("SecurityTestCollection")]
public class GrnAndUomIntegrityTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public GrnAndUomIntegrityTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task MixedPoReceipt_StockAndService_ConfirmsGrnAndPostsOnlyStockInventory()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        // 1. Master data setup
        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_MIX");
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string uid = Guid.NewGuid().ToString("N")[..8];
        var mtrUom = UnitOfMeasure.Create(tenantId, $"MTR_{uid}", "Meters", null);
        var jobUom = UnitOfMeasure.Create(tenantId, $"JOB_{uid}", "Job", null);
        await db.UnitsOfMeasure.AddRangeAsync(mtrUom, jobUom);
        await db.SaveChangesAsync();

        var stockProd = Product.Create(
            tenantId,
            $"STK_{uid}",
            $"Stock Item Cable {uid}",
            cat.CategoryId,
            unitOfMeasure: "MTR",
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: mtrUom.UnitOfMeasureId);
        await db.Products.AddAsync(stockProd);

        var srvProd = Product.Create(
            tenantId,
            $"SRV_{uid}",
            $"Installation Service {uid}",
            cat.CategoryId,
            unitOfMeasure: "JOB",
            isStockTracked: false,
            productType: ProductType.Service,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: jobUom.UnitOfMeasureId);
        await db.Products.AddAsync(srvProd);
        await db.SaveChangesAsync();

        // 2. Create Approved PO with 1 stock item and 1 service item
        string poNum = $"PO_MIX_{Guid.NewGuid():N}"[..18];
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        var stockPoItem = po.AddItem(stockProd.ProductId, quantity: 10m, unitPrice: 15.00m, unitOfMeasureId: mtrUom.UnitOfMeasureId);
        var srvPoItem = po.AddItem(srvProd.ProductId, quantity: 1m, unitPrice: 150.00m, unitOfMeasureId: jobUom.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // 3. Create draft GRN from PO
        string grnNum = $"GRN_MIX_{Guid.NewGuid():N}"[..18];
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);
        Assert.Equal(GoodsReceiptNoteStatus.Draft, grnDto.Status);
        Assert.Equal(2, grnDto.Lines.Count);

        // 4. Confirm GRN
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmedDto = await confirmRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(confirmedDto);
        Assert.Equal(GoodsReceiptNoteStatus.Confirmed, confirmedDto.Status);

        // 5. Verify PO received tracking & fulfillment
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var updatedPo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == po.Id);
        Assert.NotNull(updatedPo);
        Assert.Equal(PurchaseOrderStatus.Fulfilled, updatedPo.Status);

        var verifyStockItem = updatedPo.Items.First(i => i.Id == stockPoItem.Id);
        var verifySrvItem = updatedPo.Items.First(i => i.Id == srvPoItem.Id);
        Assert.Equal(10m, verifyStockItem.ReceivedQuantity);
        Assert.Equal(1m, verifySrvItem.ReceivedQuantity);

        // 6. Verify inventory posting: Stock item posted, Service item NOT posted
        var postingService = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var stockBalance = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, stockProd.ProductId);
        Assert.NotNull(stockBalance);
        Assert.Equal(10m, stockBalance.QuantityOnHand);

        var srvBalance = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, srvProd.ProductId);
        Assert.True(srvBalance == null || srvBalance.QuantityOnHand == 0m, "Service item must have no on-hand inventory balance.");

        var txns = await verifyDb.InventoryTransactions
            .Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId)
            .ToListAsync();
        Assert.Single(txns); // Only the stock item has an inventory transaction
        Assert.Equal(stockProd.ProductId, txns[0].ProductId);
        Assert.Equal(10m, txns[0].Quantity);
        Assert.Equal(150.00m, txns[0].TotalCost); // 10 * 15

        // 7. Verify Putaway eligibility: only stock item is eligible for putaway
        var putawayService = verifyScope.ServiceProvider.GetRequiredService<IPutawayService>();
        var eligibleLines = await putawayService.GetEligibleReceiptLinesAsync(
            tenantId,
            companyId,
            branchId,
            PutawaySourceDocumentType.GoodsReceipt,
            grnDto.GoodsReceiptNoteId);
        Assert.Single(eligibleLines);
        Assert.Equal(stockProd.ProductId, eligibleLines[0].ProductId);
        Assert.Equal(10m, eligibleLines[0].ReceivedQuantity);
    }

    [Fact]
    public async Task AlternateUomReceipt_ConvertsToBaseStockUom_AndPreservesValuation()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_UOM");
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        // 1. Setup UOM: Base EA, Alternate BOX, 1 BOX = 12 EA
        string uid = Guid.NewGuid().ToString("N")[..6];
        var uomEa = UnitOfMeasure.Create(tenantId, $"EA_{uid}", "Each Base", null);
        var uomBox = UnitOfMeasure.Create(tenantId, $"BOX_{uid}", "Box 12", null);
        await db.UnitsOfMeasure.AddRangeAsync(uomEa, uomBox);
        await db.SaveChangesAsync();

        var conv = UnitOfMeasureConversion.Create(tenantId, uomBox.UnitOfMeasureId, uomEa.UnitOfMeasureId, 12m);
        await db.UnitOfMeasureConversions.AddAsync(conv);
        await db.SaveChangesAsync();

        // 2. Product has base stock UOM = EA
        var prod = Product.Create(
            tenantId,
            $"PRD_BOX_{uid}",
            $"Widgets in Box {uid}",
            cat.CategoryId,
            unitOfMeasure: uomEa.Code,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: uomEa.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // 3. PO ordered in BOX: 2 BOX @ $60.00/BOX, total = $120.00
        string poNum = $"PO_UOM_{uid}";
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        var poItem = po.AddItem(prod.ProductId, quantity: 2m, unitPrice: 60.00m, unitOfMeasureId: uomBox.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // 4. Create draft GRN from PO (received 2 BOX)
        string grnNum = $"GRN_UOM_{uid}";
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);
        Assert.Single(grnDto.Lines);
        Assert.Equal(2m, grnDto.Lines[0].QuantityReceived);
        Assert.Equal(uomBox.UnitOfMeasureId, grnDto.Lines[0].UnitOfMeasureId);
        Assert.Equal(60.00m, grnDto.Lines[0].UnitCost);
        Assert.Equal(120.00m, grnDto.Lines[0].LineTotal);

        // 5. Confirm GRN
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // 6. Verify Base-UOM inventory posting and valuation
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var postingService = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();

        // Stock ledger balance must be 24 EA (2 BOX * 12 EA/BOX)
        var balance = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(24m, balance.QuantityOnHand);

        // Inventory transaction must record 24 EA @ $5.00/EA = $120.00 valuation
        var txns = await verifyDb.InventoryTransactions
            .Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId)
            .ToListAsync();
        Assert.Single(txns);
        Assert.Equal(24m, txns[0].Quantity);
        Assert.Equal(5.0000m, txns[0].UnitCost);
        Assert.Equal(120.00m, txns[0].TotalCost);
        Assert.Equal(uomEa.UnitOfMeasureId, txns[0].UnitOfMeasureId);

        // Original GRN document line preserves entered transaction UOM and quantity
        var confirmedGrn = await verifyDb.GoodsReceiptNotes.Include(g => g.Lines).FirstAsync(g => g.GoodsReceiptNoteId == grnDto.GoodsReceiptNoteId);
        Assert.Equal(2m, confirmedGrn.Lines[0].QuantityReceived);
        Assert.Equal(uomBox.UnitOfMeasureId, confirmedGrn.Lines[0].UnitOfMeasureId);
        Assert.Equal(60.00m, confirmedGrn.Lines[0].UnitCost);
        Assert.Equal(120.00m, confirmedGrn.Lines[0].LineTotal);
    }

    [Fact]
    public async Task AlternateUomReceipt_InverseConversion_ConvertsAccurately()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_INV");
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        // 1. Setup UOM: Base DOZ, Alternate EA. Conversion: 1 DOZ = 12 EA
        string uid = Guid.NewGuid().ToString("N")[..6];
        var uomDoz = UnitOfMeasure.Create(tenantId, $"DOZ_{uid}", "Dozen Base", null);
        var uomEa = UnitOfMeasure.Create(tenantId, $"EA_{uid}", "Each Alternate", null);
        await db.UnitsOfMeasure.AddRangeAsync(uomDoz, uomEa);
        await db.SaveChangesAsync();

        var conv = UnitOfMeasureConversion.Create(tenantId, uomDoz.UnitOfMeasureId, uomEa.UnitOfMeasureId, 12m);
        await db.UnitOfMeasureConversions.AddAsync(conv);
        await db.SaveChangesAsync();

        // 2. Product has base stock UOM = DOZ
        var prod = Product.Create(
            tenantId,
            $"PRD_DOZ_{uid}",
            $"Doughnuts {uid}",
            cat.CategoryId,
            unitOfMeasure: uomDoz.Code,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: uomDoz.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // 3. PO ordered in EA: 24 EA @ $5.00/EA = $120.00 total
        string poNum = $"PO_INV_{uid}";
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        var poItem = po.AddItem(prod.ProductId, quantity: 24m, unitPrice: 5.00m, unitOfMeasureId: uomEa.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // 4. Create draft GRN from PO (received 24 EA)
        string grnNum = $"GRN_INV_{uid}";
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);

        // 5. Confirm GRN: inverse conversion from EA to DOZ -> 24 / 12 = 2 DOZ
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // 6. Verify Base-UOM stock balance is 2 DOZ, unit cost is $60.00/DOZ, total is $120.00
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var postingService = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();

        var balance = await postingService.GetBalanceAsync(tenantId, loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(2m, balance.QuantityOnHand);

        var txns = await verifyDb.InventoryTransactions
            .Where(t => t.SourceDocumentId == grnDto.GoodsReceiptNoteId)
            .ToListAsync();
        Assert.Single(txns);
        Assert.Equal(2m, txns[0].Quantity);
        Assert.Equal(60.0000m, txns[0].UnitCost);
        Assert.Equal(120.00m, txns[0].TotalCost);
        Assert.Equal(uomDoz.UnitOfMeasureId, txns[0].UnitOfMeasureId);
    }

    [Fact]
    public async Task ProductPackagingReceipt_UsesProductSpecificPurchaseConversion()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var platformDb = sp.GetRequiredService<SaaS.Infrastructure.Persistence.SaaSDbContext>();
        var tenantConfiguration = await platformDb.TenantConfigurations.FirstAsync(c => c.TenantId == tenantId);
        tenantConfiguration.UpdateSpecialUomConversion(true);
        await platformDb.SaveChangesAsync();

        var company = await db.Companies.FirstAsync(c => c.TenantId == tenantId && c.Id == companyId);
        company.SpecialUomConversionEnabled = true;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PRODUCT_UOM");
        var category = await GetOrCreateTestCategoryAsync(db, tenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var each = UnitOfMeasure.Create(tenantId, $"EA_{suffix}", "Each", null);
        var box = UnitOfMeasure.Create(tenantId, $"BX_{suffix}", "Box", null);
        await db.UnitsOfMeasure.AddRangeAsync(each, box);
        await db.SaveChangesAsync();

        var product = Product.Create(
            tenantId,
            $"PRD_PACK_{suffix}",
            $"Packaged product {suffix}",
            category.CategoryId,
            unitOfMeasure: each.Code,
            isStockTracked: true,
            unitOfMeasureId: each.UnitOfMeasureId);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = product.ProductId,
            PackagingCode = $"PKG_{suffix}",
            PackagingName = "Twelve pieces",
            PackagingUomId = box.UnitOfMeasureId,
            BaseQuantity = 12m,
            PurchaseAllowed = true,
            CompanyId = companyId
        });

        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_PACK_{suffix}");
        var poItem = po.AddItem(product.ProductId, 10m, 120m, unitOfMeasureId: box.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });
        var draftResponse = await client.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes",
            new CreateGoodsReceiptNoteRequest
            {
                PurchaseOrderId = po.Id,
                Lines = new List<CreateGoodsReceiptNoteLineRequest>
                {
                    new()
                    {
                        PurchaseOrderItemId = poItem.Id,
                        ProductId = product.ProductId,
                        WarehouseId = warehouse.WarehouseId,
                        WarehouseLocationId = location.WarehouseLocationId,
                        QuantityReceived = 3m,
                        UnitOfMeasureId = box.UnitOfMeasureId
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(draft);
        Assert.Equal(360m, draft.Lines.Single().LineTotal);

        var confirmResponse = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{draft.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        using var verifyScope = _fixture.Services.CreateScope();
        var posting = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var balance = await posting.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(36m, balance.QuantityOnHand);
        var transaction = await verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>()
            .InventoryTransactions.SingleAsync(t => t.SourceDocumentId == draft.GoodsReceiptNoteId);
        Assert.Equal(10m, transaction.UnitCost);
        Assert.Equal(360m, transaction.TotalCost);
    }

    [Fact]
    public async Task AlternateUomReceipt_MissingConversion_ThrowsInformativeException()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_NOCONV");
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        // Setup UOM: Base EA and Unrelated KG with NO conversion defined
        string uid = Guid.NewGuid().ToString("N")[..6];
        var uomEa = UnitOfMeasure.Create(tenantId, $"EA_{uid}", "Each", null);
        var uomKg = UnitOfMeasure.Create(tenantId, $"KG_{uid}", "Kilogram", null);
        await db.UnitsOfMeasure.AddRangeAsync(uomEa, uomKg);
        await db.SaveChangesAsync();

        var prod = Product.Create(
            tenantId,
            $"PRD_NOCONV_{uid}",
            $"Nuts {uid}",
            cat.CategoryId,
            unitOfMeasure: uomEa.Code,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: uomEa.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // PO ordered in KG
        string poNum = $"PO_NOCONV_{uid}";
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        po.AddItem(prod.ProductId, quantity: 5m, unitPrice: 10.00m, unitOfMeasureId: uomKg.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        string grnNum = $"GRN_NOCONV_{uid}";
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);

        // Attempting to confirm without UOM conversion must fail cleanly with Bad Request / Conflict / Internal Error
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);

        // Must reject confirmation because conversion is missing
        Assert.True(confirmRes.StatusCode == HttpStatusCode.BadRequest ||
                    confirmRes.StatusCode == HttpStatusCode.Conflict ||
                    confirmRes.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PartialReceipts_ConvertToPoAndStockUom_AndKeepDraftOutOfReceivedQuantity()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_PARTIAL_UOM");
        var category = await GetOrCreateTestCategoryAsync(db, tenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var pcs = UnitOfMeasure.Create(tenantId, $"PCS_{suffix}", "Pieces", null);
        var box = UnitOfMeasure.Create(tenantId, $"BOX_{suffix}", "Box of twelve", null);
        await db.UnitsOfMeasure.AddRangeAsync(pcs, box);
        await db.SaveChangesAsync();
        await db.UnitOfMeasureConversions.AddAsync(
            UnitOfMeasureConversion.Create(tenantId, box.UnitOfMeasureId, pcs.UnitOfMeasureId, 12m));
        var product = Product.Create(
            tenantId,
            $"PRD_PARTIAL_{suffix}",
            $"Partial receipt product {suffix}",
            category.CategoryId,
            unitOfMeasure: pcs.Code,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None,
            unitOfMeasureId: pcs.UnitOfMeasureId);
        await db.Products.AddAsync(product);

        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_PARTIAL_{suffix}");
        var poItem = po.AddItem(
            product.ProductId,
            quantity: 100m,
            unitPrice: 12m,
            unitOfMeasureId: box.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        async Task<GoodsReceiptNoteDto> CreateReceipt(decimal qty, Guid uomId)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes",
                new CreateGoodsReceiptNoteRequest
                {
                    PurchaseOrderId = po.Id,
                    GrnNumber = $"GRN_{Guid.NewGuid():N}"[..12],
                    Lines = new List<CreateGoodsReceiptNoteLineRequest>
                    {
                        new()
                        {
                            PurchaseOrderItemId = poItem.Id,
                            ProductId = product.ProductId,
                            WarehouseId = warehouse.WarehouseId,
                            WarehouseLocationId = location.WarehouseLocationId,
                            QuantityReceived = qty,
                            UnitOfMeasureId = uomId
                        }
                    }
                });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts))!;
        }

        var firstDraft = await CreateReceipt(2m, box.UnitOfMeasureId);
        var poBeforeConfirm = await db.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == po.Id);
        Assert.Equal(0m, poBeforeConfirm.Items.Single().ReceivedQuantity);
        Assert.Empty(await db.InventoryTransactions.Where(t => t.SourceDocumentId == firstDraft.GoodsReceiptNoteId).ToListAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{firstDraft.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        var secondDraft = await CreateReceipt(3m, box.UnitOfMeasureId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{secondDraft.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        var thirdDraft = await CreateReceipt(40m, pcs.UnitOfMeasureId);
        Assert.Equal(1m, thirdDraft.Lines.Single().UnitCost);
        Assert.Equal(40m, thirdDraft.Lines.Single().LineTotal);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{thirdDraft.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var updatedPo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == po.Id);
        Assert.Equal(8.3333m, updatedPo.Items.Single().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Approved, updatedPo.Status);

        var posting = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var balance = await posting.GetBalanceAsync(tenantId, location.WarehouseLocationId, product.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(100m, balance.QuantityOnHand);
        var postedTransactions = await verifyDb.InventoryTransactions
            .Where(t => t.SourceDocumentType == SourceDocumentType.GoodsReceiptNote && t.SourceDocumentId != Guid.Empty)
            .Where(t => t.ProductId == product.ProductId)
            .ToListAsync();
        Assert.Equal(100m, postedTransactions.Sum(t => t.TotalCost));
    }

    [Fact]
    public async Task SameUomInactiveAtConfirmation_IsRejected_AndMissingUomCannotBeAssumed()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_INACTIVE_UOM");
        var category = await GetOrCreateTestCategoryAsync(db, tenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var unit = UnitOfMeasure.Create(tenantId, $"EA_{suffix}", "Each", null);
        await db.UnitsOfMeasure.AddAsync(unit);
        var product = Product.Create(
            tenantId,
            $"PRD_UOM_{suffix}",
            $"UOM test {suffix}",
            category.CategoryId,
            unitOfMeasure: unit.Code,
            isStockTracked: true,
            unitOfMeasureId: unit.UnitOfMeasureId);
        await db.Products.AddAsync(product);
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_UOM_{suffix}");
        po.AddItem(product.ProductId, 2m, 5m, unitOfMeasureId: unit.UnitOfMeasureId);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });
        var draftResponse = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?warehouseId={warehouse.WarehouseId}&warehouseLocationId={location.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(draft);

        unit.Deactivate();
        await db.SaveChangesAsync();
        var confirmResponse = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{draft.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.Conflict, confirmResponse.StatusCode);

        var noUomProduct = Product.Create(
            tenantId,
            $"PRD_NO_UOM_{suffix}",
            $"Missing UOM {suffix}",
            category.CategoryId,
            unitOfMeasure: "EA",
            isStockTracked: true);
        await db.Products.AddAsync(noUomProduct);
        var noUomPo = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_NO_UOM_{suffix}");
        noUomPo.AddItem(noUomProduct.ProductId, 1m, 1m);
        noUomPo.AutoApprove();
        await db.PurchaseOrders.AddAsync(noUomPo);
        await db.SaveChangesAsync();

        var noUomResponse = await client.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes",
            new CreateGoodsReceiptNoteRequest
            {
                PurchaseOrderId = noUomPo.Id,
                Lines = new List<CreateGoodsReceiptNoteLineRequest>
                {
                    new()
                    {
                        PurchaseOrderItemId = noUomPo.Items.Single().Id,
                        ProductId = noUomProduct.ProductId,
                        WarehouseId = warehouse.WarehouseId,
                        WarehouseLocationId = location.WarehouseLocationId,
                        QuantityReceived = 1m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Conflict, noUomResponse.StatusCode);
    }

    [Fact]
    public async Task ConvertedReceiptBoundary_UsesFullPrecisionForOverReceiptValidation()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var warehouse = await CreateTestWarehouseAsync(db, tenantId);
        var location = await CreateTestLocationAsync(db, tenantId, warehouse.WarehouseId, "BIN_UOM_BOUNDARY");
        var category = await GetOrCreateTestCategoryAsync(db, tenantId);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var each = UnitOfMeasure.Create(tenantId, $"EA_{suffix}", "Each", null);
        var box = UnitOfMeasure.Create(tenantId, $"BX_{suffix}", "Box of twelve", null);
        await db.UnitsOfMeasure.AddRangeAsync(each, box);
        await db.SaveChangesAsync();
        await db.UnitOfMeasureConversions.AddAsync(
            UnitOfMeasureConversion.Create(tenantId, box.UnitOfMeasureId, each.UnitOfMeasureId, 12m));
        var product = Product.Create(
            tenantId,
            $"PRD_BOUNDARY_{suffix}",
            $"Boundary {suffix}",
            category.CategoryId,
            unitOfMeasure: each.Code,
            isStockTracked: true,
            unitOfMeasureId: each.UnitOfMeasureId);
        await db.Products.AddAsync(product);
        var representablePo = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_BELOW_{suffix}");
        var representableItem = representablePo.AddItem(product.ProductId, 10m, 12m, unitOfMeasureId: box.UnitOfMeasureId);
        representablePo.AutoApprove();
        var cumulativePo = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_CUM_{suffix}");
        var cumulativeItem = cumulativePo.AddItem(product.ProductId, 10m, 12m, unitOfMeasureId: box.UnitOfMeasureId);
        cumulativePo.AutoApprove();
        var abovePo = PurchaseOrder.Create(tenantId, companyId, branchId, $"PO_ABOVE_{suffix}");
        var aboveItem = abovePo.AddItem(product.ProductId, 10m, 12m, unitOfMeasureId: box.UnitOfMeasureId);
        abovePo.AutoApprove();
        await db.PurchaseOrders.AddRangeAsync(representablePo, cumulativePo, abovePo);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        async Task<GoodsReceiptNoteDto> CreateReceipt(PurchaseOrder po, PurchaseOrderItem item, decimal qty)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes",
                new CreateGoodsReceiptNoteRequest
                {
                    PurchaseOrderId = po.Id,
                    Lines = new List<CreateGoodsReceiptNoteLineRequest>
                    {
                        new()
                        {
                            PurchaseOrderItemId = item.Id,
                            ProductId = product.ProductId,
                            WarehouseId = warehouse.WarehouseId,
                            WarehouseLocationId = location.WarehouseLocationId,
                            QuantityReceived = qty,
                            UnitOfMeasureId = each.UnitOfMeasureId
                        }
                    }
                });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts))!;
        }

        var belowDraft = await CreateReceipt(representablePo, representableItem, 119.999m);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{belowDraft.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        var cumulativeFirst = await CreateReceipt(cumulativePo, cumulativeItem, 119.9999m);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{cumulativeFirst.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);
        using (var firstReceiptScope = _fixture.Services.CreateScope())
        {
            var firstReceiptDb = firstReceiptScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var partiallyReceivedPo = await firstReceiptDb.PurchaseOrders
                .Include(order => order.Items)
                .FirstAsync(order => order.Id == cumulativePo.Id);
            Assert.Equal(9.9999m, partiallyReceivedPo.Items.Single().ReceivedQuantity);
            Assert.Equal(PurchaseOrderStatus.Approved, partiallyReceivedPo.Status);
        }

        var cumulativeFinal = await CreateReceipt(cumulativePo, cumulativeItem, 0.0001m);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{cumulativeFinal.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        var aboveDraft = await CreateReceipt(abovePo, aboveItem, 120.0001m);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{aboveDraft.GoodsReceiptNoteId}/confirm",
            null)).StatusCode);

        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var updatedRepresentablePo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == representablePo.Id);
        Assert.Equal(9.9999m, updatedRepresentablePo.Items.Single().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Approved, updatedRepresentablePo.Status);
        var updatedCumulativePo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == cumulativePo.Id);
        Assert.Equal(10m, updatedCumulativePo.Items.Single().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Fulfilled, updatedCumulativePo.Status);
        var cumulativeTransactions = await verifyDb.InventoryTransactions
            .Where(transaction => transaction.SourceDocumentId == cumulativeFirst.GoodsReceiptNoteId ||
                                  transaction.SourceDocumentId == cumulativeFinal.GoodsReceiptNoteId)
            .ToListAsync();
        Assert.Equal(120m, cumulativeTransactions.Sum(transaction => transaction.Quantity));
        var updatedAbovePo = await verifyDb.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == abovePo.Id);
        Assert.Equal(0m, updatedAbovePo.Items.Single().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.Approved, updatedAbovePo.Status);
    }

    [Fact]
    public void TrackingPolicyValidation_RejectsIncompatibleCombinations()
    {
        var tenantId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        // 1. Service configured as stock tracked
        var exService = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                "SRV_INVALID",
                "Service Stock",
                catId,
                unitOfMeasure: "HRS",
                isStockTracked: true,
                productType: ProductType.Service,
                trackingMode: TrackingMode.None));
        Assert.Contains("cannot be configured as a stock-tracked item", exService.Message);

        // 2. Non-stock item configured as stock tracked
        var exNonStock = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                "NONSTOCK_INVALID",
                "Non-stock Stock",
                catId,
                unitOfMeasure: "EA",
                isStockTracked: true,
                productType: ProductType.NonStockItem,
                trackingMode: TrackingMode.None));
        Assert.Contains("cannot be configured as a stock-tracked item", exNonStock.Message);

        // 3. Not stock tracked with Batch tracking
        var exBatch = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                "NOTRACK_BATCH",
                "No Track Batch",
                catId,
                unitOfMeasure: "EA",
                isStockTracked: false,
                productType: ProductType.NonStockItem,
                trackingMode: TrackingMode.Batch));
        Assert.Contains("cannot have tracking mode", exBatch.Message);

        // 4. Not stock tracked with Serial tracking
        var exSerial = Assert.Throws<InvalidOperationException>(() =>
            Product.Create(
                tenantId,
                "NOTRACK_SERIAL",
                "No Track Serial",
                catId,
                unitOfMeasure: "EA",
                isStockTracked: false,
                productType: ProductType.NonStockItem,
                trackingMode: TrackingMode.Serial));
        Assert.Contains("cannot have tracking mode", exSerial.Message);

        // 5. Update validation
        var validProd = Product.Create(
            tenantId,
            "VALID_STOCK",
            "Valid Stock",
            catId,
            unitOfMeasure: "EA",
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.None);

        Assert.Throws<InvalidOperationException>(() =>
            validProd.Update(
                "Updated Name",
                catId,
                "EA",
                description: null,
                ValuationMethod.FIFO,
                standardCost: 10m,
                sellingPrice: 20m,
                reorderLevel: 0,
                reorderQuantity: 0,
                isManufacturable: false,
                isPurchasable: true,
                isSellable: true,
                canConsumeInProduction: false,
                canConsumeInMaintenance: false,
                isStockTracked: false,
                ProductType.NonStockItem,
                TrackingMode.Batch)); // Incompatible update

        // 6. Valid configurations succeed
        var validBatch = Product.Create(
            tenantId,
            "VALID_BATCH",
            "Valid Batch Product",
            catId,
            unitOfMeasure: "EA",
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: TrackingMode.Batch);
        Assert.NotNull(validBatch);

        var validService = Product.Create(
            tenantId,
            "VALID_SRV",
            "Valid Service Product",
            catId,
            unitOfMeasure: "HRS",
            isStockTracked: false,
            productType: ProductType.Service,
            trackingMode: TrackingMode.None);
        Assert.NotNull(validService);
    }

    // Helper methods
    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18];
        var wh = Warehouse.Create(
            tenantId,
            code,
            $"Warehouse_{code}",
            companyId: TestConstants.CompanyAId,
            branchId: TestConstants.BranchA1Id,
            city: "Colombo");
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

    private static async Task<Domain.Features.MasterData.Category.Category> GetOrCreateTestCategoryAsync(DomainDbContext db, Guid tenantId)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (cat == null)
        {
            cat = Domain.Features.MasterData.Category.Category.Create(
                tenantId,
                $"CAT_{Guid.NewGuid():N}"[..18],
                "Test Category");
            await db.Categories.AddAsync(cat);
            await db.SaveChangesAsync();
        }
        return cat;
    }
}
