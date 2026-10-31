using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Category;
using Domain.Features.MasterData.Company;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Warehouse;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class ProductPackagingAndSpecialUomTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProductPackagingAndSpecialUomTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CommonUomConversion_UniversalRatiosAndInverse_WorkAccurately()
    {
        using var scope = _fixture.Services.CreateScope();
        var uomService = scope.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;

        var uomDozen = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "DOZEN");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        // Direct: 5 DOZEN -> PCS (12 * 5 = 60)
        var directResult = await uomService.ConvertQuantityAsync(tenantId, uomDozen.UnitOfMeasureId, uomPcs.UnitOfMeasureId, 5m);
        Assert.Equal(60m, directResult.ConvertedQuantity);
        Assert.Equal(12m, directResult.EffectiveFactor);
        Assert.False(directResult.IsInverse);

        // Inverse: 60 PCS -> DOZEN (60 / 12 = 5)
        var inverseResult = await uomService.ConvertQuantityAsync(tenantId, uomPcs.UnitOfMeasureId, uomDozen.UnitOfMeasureId, 60m);
        Assert.Equal(5m, inverseResult.ConvertedQuantity);
        Assert.True(inverseResult.IsInverse);
    }

    [Fact]
    public async Task ProductSpecificPackaging_TwoProductsShareBoxToPcs_ResolveIndependently()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var uomService = scope.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();
        var tenantId = TestConstants.TenantAlphaId;

        // 1. Enable Special UOM Conversion on tenant
        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyAId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyBId, true);

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..6];

        // Product A: Bandages (Base UOM = PCS)
        var prodA = Product.Create(
            tenantId,
            $"PHA_BND_{uid}",
            $"Bandages Box {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isStockTracked: true,
            productType: ProductType.StockItem);
        await db.Products.AddAsync(prodA);

        // Product B: Screws (Base UOM = PCS)
        var prodB = Product.Create(
            tenantId,
            $"HDW_SCR_{uid}",
            $"Screws Box {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isStockTracked: true,
            productType: ProductType.StockItem);
        await db.Products.AddAsync(prodB);
        await db.SaveChangesAsync();

        // 2. Define Packaging: Product A has 1 BOX = 20 PCS
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prodA.ProductId,
            PackagingCode = $"BND-BOX-20-{uid}",
            PackagingName = "Bandage Box 20 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 20m,
            PurchaseAllowed = true,
            SalesAllowed = true
        });

        // 3. Define Packaging: Product B has 1 BOX = 200 PCS
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prodB.ProductId,
            PackagingCode = $"SCR-BOX-200-{uid}",
            PackagingName = "Screw Box 200 Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 200m,
            PurchaseAllowed = true,
            SalesAllowed = true
        });

        // 4. Resolve Product A: 3 BOX -> 60 PCS
        var resA = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prodA.ProductId,
            uomBox.UnitOfMeasureId,
            uomPcs.UnitOfMeasureId,
            3m,
            ConversionContext.Purchase);

        Assert.True(resA.IsSpecialConversion);
        Assert.Equal(60m, resA.ConvertedQuantity);
        Assert.Equal(20m, resA.EffectiveFactor);
        Assert.False(resA.IsInverse);

        // 5. Resolve Product B: 3 BOX -> 600 PCS
        var resB = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prodB.ProductId,
            uomBox.UnitOfMeasureId,
            uomPcs.UnitOfMeasureId,
            3m,
            ConversionContext.Purchase);

        Assert.True(resB.IsSpecialConversion);
        Assert.Equal(600m, resB.ConvertedQuantity);
        Assert.Equal(200m, resB.EffectiveFactor);
        Assert.False(resB.IsInverse);

        // 6. Inverse: 40 PCS of Product A -> 2 BOX
        var invA = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prodA.ProductId,
            uomPcs.UnitOfMeasureId,
            uomBox.UnitOfMeasureId,
            40m,
            ConversionContext.Sales);

        Assert.True(invA.IsSpecialConversion);
        Assert.Equal(2m, invA.ConvertedQuantity);
        Assert.True(invA.IsInverse);
    }

    [Fact]
    public async Task SpecialConversion_DisabledByDefault_RejectsExplicitPackagingId()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var uomService = scope.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();
        var tenantId = TestConstants.TenantAlphaId;

        // Ensure special conversion is DISABLED
        await EnableSpecialUomAsync(tenantId, false);

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..6];
        var prod = Product.Create(
            tenantId,
            $"DIS_{uid}",
            $"Disabled Special Prod {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        var pkg = await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod.ProductId,
            PackagingCode = $"PKG-DIS-{uid}",
            PackagingName = "Disabled Packaging",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 25m
        });

        // Requesting with explicit packaging ID while feature disabled -> throws InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uomService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                2m,
                ConversionContext.General,
                productPackagingId: pkg.ProductPackagingId));

        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiplePackagingSizes_SameProductAndUom_DisambiguatedByPackagingId()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var uomService = scope.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();
        var tenantId = TestConstants.TenantAlphaId;

        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyAId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyBId, true);

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..6];
        var prod = Product.Create(
            tenantId,
            $"MULTI_PKG_{uid}",
            $"Multi Size Product {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // Package 1: Small Box = 10 PCS
        var pkgSmall = await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod.ProductId,
            PackagingCode = $"BOX-10-{uid}",
            PackagingName = "Small Box 10 Count",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 10m
        });

        // Package 2: Large Box = 50 PCS
        var pkgLarge = await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod.ProductId,
            PackagingCode = $"BOX-50-{uid}",
            PackagingName = "Large Box 50 Count",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 50m
        });

        // Calling without packaging ID when multiple exist -> Ambiguous exception
        var ambigEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uomService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                2m));
        Assert.Contains("Multiple active packaging definitions exist", ambigEx.Message);

        // Specifying small box -> 2 * 10 = 20
        var resSmall = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prod.ProductId,
            uomBox.UnitOfMeasureId,
            uomPcs.UnitOfMeasureId,
            2m,
            productPackagingId: pkgSmall.ProductPackagingId);
        Assert.Equal(20m, resSmall.ConvertedQuantity);

        // Specifying large box -> 2 * 50 = 100
        var resLarge = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prod.ProductId,
            uomBox.UnitOfMeasureId,
            uomPcs.UnitOfMeasureId,
            2m,
            productPackagingId: pkgLarge.ProductPackagingId);
        Assert.Equal(100m, resLarge.ConvertedQuantity);
    }

    [Fact]
    public async Task CompanySpecificPackaging_EnforcesCompanyEligibility()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var uomService = scope.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();
        var tenantId = TestConstants.TenantAlphaId;

        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyAId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyBId, true);

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..6];
        var prod = Product.Create(
            tenantId,
            $"COMP_PKG_{uid}",
            $"Company Packaged Item {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // Packaging is restricted to Company A only
        var pkg = await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod.ProductId,
            PackagingCode = $"COMP-A-BOX-{uid}",
            PackagingName = "Company A Exclusive Box",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 15m,
            CompanyId = TestConstants.CompanyAId
        });

        // Conversion under Company A -> succeeds
        var resCompA = await uomService.ConvertProductQuantityAsync(
            tenantId,
            prod.ProductId,
            uomBox.UnitOfMeasureId,
            uomPcs.UnitOfMeasureId,
            2m,
            productPackagingId: pkg.ProductPackagingId,
            companyId: TestConstants.CompanyAId);
        Assert.Equal(30m, resCompA.ConvertedQuantity);

        // Conversion under Company B -> rejected with eligibility error
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            uomService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                2m,
                productPackagingId: pkg.ProductPackagingId,
                companyId: TestConstants.CompanyBId));

        Assert.Contains("not eligible for company", ex.Message);
    }

    [Fact]
    public async Task ScenarioA_TenTenants_TenIndustries_TenantIsolationVerified()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();

        var industries = new[]
        {
            "Retail", "Wholesale", "ImportExport", "Hardware", "Industrial",
            "Pharmacy", "Apparel", "Electronics", "Manufacturing", "Services"
        };

        var tenantIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            // Deterministic isolated tenant ID per industry
            var tId = Guid.NewGuid();
            tenantIds.Add(tId);
            await EnableSpecialUomAsync(tId, true);

            string uid = Guid.NewGuid().ToString("N")[..6];

            // Seed Category & UOMs for this tenant
            var cat = Category.Create(tId, $"CAT_{industries[i]}_{uid}", $"{industries[i]} Category {uid}");
            var uomBox = UnitOfMeasure.Create(tId, $"BOX_{uid}", "Box Unit");
            var uomPcs = UnitOfMeasure.Create(tId, $"PCS_{uid}", "Piece Unit");
            await db.Categories.AddAsync(cat);
            await db.UnitsOfMeasure.AddRangeAsync(uomBox, uomPcs);
            await db.SaveChangesAsync();

            // Seed Product for this tenant
            var prod = Product.Create(
                tId,
                $"PRD_{industries[i]}_{uid}",
                $"{industries[i]} Sample Product",
                cat.CategoryId,
                unitOfMeasure: uomPcs.Code,
                unitOfMeasureId: uomPcs.UnitOfMeasureId);
            await db.Products.AddAsync(prod);
            await db.SaveChangesAsync();

            // Seed Special Packaging for this tenant
            await packagingService.CreatePackagingAsync(tId, new CreateProductPackagingRequest
            {
                ProductId = prod.ProductId,
                PackagingCode = $"PKG-{industries[i]}-{uid}",
                PackagingName = $"{industries[i]} Package 10",
                PackagingUomId = uomBox.UnitOfMeasureId,
                BaseQuantity = 10m * (i + 1)
            });
        }

        // Verify Tenant Isolation: Querying Tenant 0 returns zero packagings of any other tenant
        for (int i = 0; i < 10; i++)
        {
            var packagings = await packagingService.GetPackagingsAsync(tenantIds[i]);
            Assert.Single(packagings);
            Assert.Equal(tenantIds[i], packagings[0].TenantId);
            Assert.Contains(industries[i].ToUpperInvariant(), packagings[0].PackagingCode);
        }
    }

    [Fact]
    public async Task ScenarioB_OneTenant_FiveCompanies_ProcurementFulfillmentAndServiceIntegrity()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;

        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyAId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyBId, true);

        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var postingService = sp.GetRequiredService<IInventoryPostingService>();

        // 1. Setup Warehouse & Location
        var wh = await CreateTestWarehouseAsync(db, tenantId, companyId, branchId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "LOC_SCEN_B");
        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);

        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..6];

        // 2. Physical Stock Item: Product A (1 BOX = 20 PCS)
        var stockProd = Product.Create(
            tenantId,
            $"STK_B_{uid}",
            $"Physical Good {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isStockTracked: true,
            productType: ProductType.StockItem);
        await db.Products.AddAsync(stockProd);

        // 3. Service Product: Cannot create stock movements
        var srvProd = Product.Create(
            tenantId,
            $"SRV_B_{uid}",
            $"Consulting Service {uid}",
            cat.CategoryId,
            unitOfMeasure: "JOB",
            isStockTracked: false,
            productType: ProductType.Service);
        await db.Products.AddAsync(srvProd);
        await db.SaveChangesAsync();

        // Packaging: 1 BOX = 20 PCS
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = stockProd.ProductId,
            PackagingCode = $"BOX20-SCEN-{uid}",
            PackagingName = "20-Pack Box",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = 20m,
            PurchaseAllowed = true,
            SalesAllowed = true
        });

        // 4. Create PO with 2 BOX of stock item @ $100/BOX ($5/PC) and 1 JOB of service item @ $250
        string poNum = $"PO_B_{uid}";
        var po = PurchaseOrder.Create(tenantId, companyId, branchId, poNum);
        po.AddItem(stockProd.ProductId, quantity: 2m, unitPrice: 100.00m, unitOfMeasureId: uomBox.UnitOfMeasureId);
        po.AddItem(srvProd.ProductId, quantity: 1m, unitPrice: 250.00m);
        po.AutoApprove();
        await db.PurchaseOrders.AddAsync(po);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            tenantId,
            new[] { "Branch User" },
            new[] { "GRN.VIEW", "GRN.CREATE", "GRN.CONFIRM" });

        // 5. Create Draft GRN from PO
        string grnNum = $"GRN_B_{uid}";
        var createRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{po.Id}?grnNumber={grnNum}&warehouseId={wh.WarehouseId}&warehouseLocationId={loc.WarehouseLocationId}",
            null);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var grnDto = await createRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts);
        Assert.NotNull(grnDto);

        // 6. Confirm GRN
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grnDto.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // 7. Verify Inventory Ledger:
        // Physical product must be posted in base UOM (PCS): 2 BOX * 20 PCS/BOX = 40 PCS
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyPosting = verifyScope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var balance = await verifyPosting.GetBalanceAsync(tenantId, loc.WarehouseLocationId, stockProd.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(40m, balance.QuantityOnHand);

        // Service product has NO inventory balance
        var srvBalance = await verifyPosting.GetBalanceAsync(tenantId, loc.WarehouseLocationId, srvProd.ProductId);
        Assert.Null(srvBalance);
    }

    [Fact]
    public async Task CompanyLevelSpecialUom_TenScenarios_Verified()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var uomService = sp.GetRequiredService<IUnitOfMeasureService>();
        var packagingService = sp.GetRequiredService<IProductPackagingService>();
        var tenantId = TestConstants.TenantAlphaId;
        var compA = TestConstants.CompanyAId;
        var compB = TestConstants.CompanyBId;
        var crossTenantComp = TestConstants.CompanyCId; // Belongs to TenantBeta

        // Scenario 9: Existing companies retain default true after migration
        var existingCompA = await db.Companies.FirstAsync(c => c.TenantId == tenantId && c.Id == compA);
        Assert.True(existingCompA.SpecialUomConversionEnabled);

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");
        var uomDozen = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "DOZEN");

        string uid = Guid.NewGuid().ToString("N")[..6];
        var prod = Product.Create(
            tenantId,
            $"CO_UOM_{uid}",
            $"Company Setting Prod {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId);
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();

        // Configure: Company A = Enabled, Company B = Disabled
        await SetCompanySpecialUomAsync(tenantId, compA, true);
        await SetCompanySpecialUomAsync(tenantId, compB, false);

        try
        {
            // --- Scenario 1: SaaS disabled + company enabled -> special conversion rejected ---
        await EnableSpecialUomAsync(tenantId, false);

        using (var s1 = _fixture.Services.CreateScope())
        {
            var pService = s1.ServiceProvider.GetRequiredService<IProductPackagingService>();
            // Trying to create packaging for Company A while SaaS is disabled is blocked
            var saasDisPkgEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
                {
                    ProductId = prod.ProductId,
                    PackagingCode = $"PKG-SAAS-DIS-{uid}",
                    PackagingName = "SaaS Disabled Box",
                    PackagingUomId = uomBox.UnitOfMeasureId,
                    BaseQuantity = 20m,
                    CompanyId = compA
                }));
            Assert.Contains("disabled", saasDisPkgEx.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Now enable SaaS at tenant level
        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, compA, true);
        await SetCompanySpecialUomAsync(tenantId, compB, false);

        Guid pkgAId;
        Guid pkgSharedId;

        using (var s2 = _fixture.Services.CreateScope())
        {
            var pService = s2.ServiceProvider.GetRequiredService<IProductPackagingService>();

            // --- Scenario 5: Product Master special packaging configuration is blocked when disabled and permitted when enabled ---
            // Blocked for Company B (disabled)
            var compDisPkgEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
                {
                    ProductId = prod.ProductId,
                    PackagingCode = $"PKG-B-DIS-{uid}",
                    PackagingName = "Company B Blocked Box",
                    PackagingUomId = uomBox.UnitOfMeasureId,
                    BaseQuantity = 20m,
                    CompanyId = compB
                }));
            Assert.Contains("disabled for the specified company", compDisPkgEx.Message, StringComparison.OrdinalIgnoreCase);

            // Permitted for Company A (enabled)
            var pkgA = await pService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
            {
                ProductId = prod.ProductId,
                PackagingCode = $"PKG-A-EN-{uid}",
                PackagingName = "Company A Allowed Box",
                PackagingUomId = uomBox.UnitOfMeasureId,
                BaseQuantity = 20m,
                CompanyId = compA
            });
            Assert.NotNull(pkgA);
            pkgAId = pkgA.ProductPackagingId;

            // Also create a shared (tenant-wide, CompanyId = null) packaging definition (1 BOX = 25 PCS)
            var pkgShared = await pService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
            {
                ProductId = prod.ProductId,
                PackagingCode = $"PKG-SHR-{uid}",
                PackagingName = "Shared 25 Box",
                PackagingUomId = uomBox.UnitOfMeasureId,
                BaseQuantity = 25m,
                CompanyId = null
            });
            Assert.NotNull(pkgShared);
            pkgSharedId = pkgShared.ProductPackagingId;
        }

        using (var s3 = _fixture.Services.CreateScope())
        {
            var uService = s3.ServiceProvider.GetRequiredService<IUnitOfMeasureService>();

            // --- Scenario 2: SaaS enabled + Company A enabled -> special conversion succeeds ---
            var convA = await uService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                3m,
                productPackagingId: pkgAId,
                companyId: compA);
            Assert.True(convA.IsSpecialConversion);
            Assert.Equal(60m, convA.ConvertedQuantity); // 3 * 20 = 60

            // --- Scenario 3: SaaS enabled + Company B disabled in the same tenant -> special conversion rejected ---
            var convBEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                uService.ConvertProductQuantityAsync(
                    tenantId,
                    prod.ProductId,
                    uomBox.UnitOfMeasureId,
                    uomPcs.UnitOfMeasureId,
                    3m,
                    productPackagingId: pkgSharedId,
                    companyId: compB));
            Assert.Contains("disabled", convBEx.Message, StringComparison.OrdinalIgnoreCase);

            // --- Scenario 4: Company B uses common DOZEN -> PCS conversion -> succeeds ---
            var commonB = await uService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomDozen.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                5m,
                companyId: compB);
            Assert.False(commonB.IsSpecialConversion);
            Assert.Equal(60m, commonB.ConvertedQuantity); // 5 * 12 = 60

            // --- Scenario 6: Invalid or cross-tenant company ID -> safely rejected ---
            var crossTenantEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                uService.ConvertProductQuantityAsync(
                    tenantId,
                    prod.ProductId,
                    uomBox.UnitOfMeasureId,
                    uomPcs.UnitOfMeasureId,
                    3m,
                    productPackagingId: pkgSharedId,
                    companyId: crossTenantComp));
            Assert.Contains("invalid or does not belong", crossTenantEx.Message, StringComparison.OrdinalIgnoreCase);

            var randomInvalidCompEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                uService.ConvertProductQuantityAsync(
                    tenantId,
                    prod.ProductId,
                    uomBox.UnitOfMeasureId,
                    uomPcs.UnitOfMeasureId,
                    3m,
                    productPackagingId: pkgSharedId,
                    companyId: Guid.NewGuid()));
            Assert.Contains("invalid or does not belong", randomInvalidCompEx.Message, StringComparison.OrdinalIgnoreCase);

            // --- Scenario 7: Multiple packaging sizes for same product/UOM still require packaging ID ---
            var ambigEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                uService.ConvertProductQuantityAsync(
                    tenantId,
                    prod.ProductId,
                    uomBox.UnitOfMeasureId,
                    uomPcs.UnitOfMeasureId,
                    2m,
                    companyId: compA));
            Assert.Contains("Multiple active packaging definitions exist", ambigEx.Message);

            // --- Scenario 8: Existing product-specific conversions remain correct ---
            var specificA = await uService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                2m,
                productPackagingId: pkgAId,
                companyId: compA);
            Assert.Equal(40m, specificA.ConvertedQuantity);

            var specificShared = await uService.ConvertProductQuantityAsync(
                tenantId,
                prod.ProductId,
                uomBox.UnitOfMeasureId,
                uomPcs.UnitOfMeasureId,
                2m,
                productPackagingId: pkgSharedId,
                companyId: compA);
            Assert.Equal(50m, specificShared.ConvertedQuantity);
        }
    }
    finally
    {
        // Clean up company settings for future test runs
        await SetCompanySpecialUomAsync(tenantId, compB, true);
    }
}

    private async Task SetCompanySpecialUomAsync(Guid tenantId, Guid companyId, bool enabled)
    {
        using var scope = _fixture.Services.CreateScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var company = await domainDb.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId);
        if (company != null)
        {
            company.SpecialUomConversionEnabled = enabled;
            await domainDb.SaveChangesAsync();
        }
    }

    private async Task EnableSpecialUomAsync(Guid tenantId, bool enabled)
    {
        using var scope = _fixture.Services.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<SaaS.Infrastructure.Persistence.SaaSDbContext>();

        if (!await platformDb.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            platformDb.Tenants.Add(new SaaS.Core.Models.Tenant
            {
                Id = tenantId,
                Name = $"Tenant {tenantId:N}"[..18],
                Code = $"T_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                IsActive = true
            });
            await platformDb.SaveChangesAsync();
        }

        var config = await platformDb.TenantConfigurations.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (config == null)
        {
            config = SaaS.Core.Models.TenantConfiguration.CreateDefault(tenantId);
            config.UpdateSpecialUomConversion(enabled);
            await platformDb.TenantConfigurations.AddAsync(config);
        }
        else
        {
            config.UpdateSpecialUomConversion(enabled);
        }
        await platformDb.SaveChangesAsync();
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId, Guid companyId, Guid branchId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var wh = Warehouse.Create(
            tenantId,
            code,
            $"Warehouse {code}",
            companyId: companyId,
            branchId: branchId);
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId, string code)
    {
        string uniqueCode = $"{code}_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var loc = WarehouseLocation.Create(tenantId, warehouseId, null, uniqueCode, $"Location {uniqueCode}");
        await db.WarehouseLocations.AddAsync(loc);
        await db.SaveChangesAsync();
        return loc;
    }
}
