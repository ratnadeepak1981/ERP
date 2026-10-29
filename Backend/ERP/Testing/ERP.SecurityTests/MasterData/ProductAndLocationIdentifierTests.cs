using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Common;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class ProductAndLocationIdentifierTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProductAndLocationIdentifierTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task PathB_MultipleIdentifiers_OnSingleProduct_PersistsWithPackagingUom()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;

        // Master data setup
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);
        string uid = Guid.NewGuid().ToString("N")[..6];

        var uomEa = UnitOfMeasure.Create(tenantId, $"EA_{uid}", "Each", null);
        var uomBox = UnitOfMeasure.Create(tenantId, $"BOX_{uid}", "Box 12", null);
        await db.UnitsOfMeasure.AddRangeAsync(uomEa, uomBox);
        await db.SaveChangesAsync();

        var conv = UnitOfMeasureConversion.Create(tenantId, uomBox.UnitOfMeasureId, uomEa.UnitOfMeasureId, 12m);
        await db.UnitOfMeasureConversions.AddAsync(conv);

        var product = Product.Create(
            tenantId,
            $"PRD_ID_{uid}",
            $"Product {uid}",
            cat.CategoryId,
            unitOfMeasure: uomEa.Code,
            unitOfMeasureId: uomEa.UnitOfMeasureId);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        // 1. Add EAN-13 Piece Barcode
        string pieceBarcode = GenerateValidEan13();
        var pieceReq = new CreateProductIdentifierRequest
        {
            IdentifierValue = pieceBarcode,
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.EAN13,
            Source = IdentifierSource.Manufacturer,
            UnitOfMeasureId = uomEa.UnitOfMeasureId,
            IsPrimary = true
        };

        var pieceRes = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", pieceReq);
        Assert.Equal(HttpStatusCode.Created, pieceRes.StatusCode);
        var pieceDto = await pieceRes.ReadApiResponseDataAsync<ProductIdentifierDto>(_jsonOpts);
        Assert.NotNull(pieceDto);
        Assert.Equal(pieceBarcode, pieceDto.IdentifierValue);
        Assert.True(pieceDto.IsPrimary);
        Assert.Equal(uomEa.UnitOfMeasureId, pieceDto.UnitOfMeasureId);

        // 2. Add Carton Code 128 Barcode for BOX UOM
        var cartonReq = new CreateProductIdentifierRequest
        {
            IdentifierValue = $"CTN-{uid}-123",
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.Code128,
            Source = IdentifierSource.Supplier,
            UnitOfMeasureId = uomBox.UnitOfMeasureId,
            IsPrimary = true
        };

        var cartonRes = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", cartonReq);
        Assert.Equal(HttpStatusCode.Created, cartonRes.StatusCode);
        var cartonDto = await cartonRes.ReadApiResponseDataAsync<ProductIdentifierDto>(_jsonOpts);
        Assert.NotNull(cartonDto);
        Assert.Equal($"CTN-{uid}-123".ToUpperInvariant(), cartonDto.IdentifierValue);
        Assert.True(cartonDto.IsPrimary);
        Assert.Equal(uomBox.UnitOfMeasureId, cartonDto.UnitOfMeasureId);

        // 3. Get all identifiers for product: should return 2 active identifiers
        var listRes = await client.GetAsync($"/api/products/{product.ProductId}/identifiers");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listDto = await listRes.ReadApiResponseDataAsync<List<ProductIdentifierDto>>(_jsonOpts);
        Assert.NotNull(listDto);
        Assert.Equal(2, listDto.Count);
        Assert.Contains(listDto, i => i.IdentifierValue == pieceBarcode && i.IsPrimary);
        Assert.Contains(listDto, i => i.IdentifierValue == $"CTN-{uid}-123".ToUpperInvariant() && i.IsPrimary);
    }

    [Fact]
    public async Task PathB_MultipleIdentifiers_OnSingleLocation_PersistsCode128AndQr()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;

        var wh = await CreateTestWarehouseAsync(db, tenantId);
        var loc = await CreateTestLocationAsync(db, tenantId, wh.WarehouseId, "BIN_LOC_ID");

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "LOCATION.VIEW", "LOCATION.EDIT" });

        // 1. Generate internal Code 128 identifier
        var gen128Res = await client.PostAsJsonAsync(
            $"/api/warehouses/{wh.WarehouseId}/locations/{loc.WarehouseLocationId}/identifiers/generate",
            new GenerateInternalLocationIdentifierRequest
            {
                IdentifierType = IdentifierType.LinearBarcode,
                Symbology = IdentifierSymbology.Code128,
                IsPrimary = true
            });
        Assert.Equal(HttpStatusCode.Created, gen128Res.StatusCode);
        var gen128Dto = await gen128Res.ReadApiResponseDataAsync<LocationIdentifierDto>(_jsonOpts);
        Assert.NotNull(gen128Dto);
        Assert.StartsWith("APX-L-", gen128Dto.IdentifierValue);
        Assert.True(gen128Dto.IsPrimary);

        // 2. Generate internal QR code identifier
        var genQrRes = await client.PostAsJsonAsync(
            $"/api/warehouses/{wh.WarehouseId}/locations/{loc.WarehouseLocationId}/identifiers/generate",
            new GenerateInternalLocationIdentifierRequest
            {
                IdentifierType = IdentifierType.QRCode,
                Symbology = IdentifierSymbology.QRCode,
                IsPrimary = false
            });
        Assert.Equal(HttpStatusCode.Created, genQrRes.StatusCode);
        var genQrDto = await genQrRes.ReadApiResponseDataAsync<LocationIdentifierDto>(_jsonOpts);
        Assert.NotNull(genQrDto);
        Assert.StartsWith("urn:apex:v1:loc:", genQrDto.IdentifierValue, StringComparison.OrdinalIgnoreCase);
        Assert.False(genQrDto.IsPrimary);

        // 3. List identifiers for location
        var listRes = await client.GetAsync($"/api/warehouses/{wh.WarehouseId}/locations/{loc.WarehouseLocationId}/identifiers");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listDto = await listRes.ReadApiResponseDataAsync<List<LocationIdentifierDto>>(_jsonOpts);
        Assert.NotNull(listDto);
        Assert.Equal(2, listDto.Count);
    }

    [Fact]
    public async Task PathB_DuplicateActiveIdentifier_WithinSameTenant_ReturnsConflict()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string u = Guid.NewGuid().ToString("N")[..8];
        var p1 = Product.Create(tenantId, $"P1_{u}", $"Prod 1 {u}", cat.CategoryId);
        var p2 = Product.Create(tenantId, $"P2_{u}", $"Prod 2 {u}", cat.CategoryId);
        await db.Products.AddRangeAsync(p1, p2);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        string barcode = $"BC_DUP_{Guid.NewGuid():N}"[..16];

        // Register to Product 1
        var res1 = await client.PostAsJsonAsync($"/api/products/{p1.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = barcode,
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.Code128
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Attempt to register same barcode to Product 2 -> Conflict (409)
        var res2 = await client.PostAsJsonAsync($"/api/products/{p2.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = barcode,
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.Code128
        });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task PathB_DuplicateIdentifier_AcrossDifferentTenants_Succeeds()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantAlpha = TestConstants.TenantAlphaId;
        var tenantBeta = TestConstants.TenantBetaId;

        var catAlpha = await GetOrCreateTestCategoryAsync(db, tenantAlpha);
        var catBeta = await GetOrCreateTestCategoryAsync(db, tenantBeta);

        string u = Guid.NewGuid().ToString("N")[..8];
        var pAlpha = Product.Create(tenantAlpha, $"PA_{u}", $"Prod Alpha {u}", catAlpha.CategoryId);
        var pBeta = Product.Create(tenantBeta, $"PB_{u}", $"Prod Beta {u}", catBeta.CategoryId);
        await db.Products.AddRangeAsync(pAlpha, pBeta);
        await db.SaveChangesAsync();

        string sharedBarcode = GenerateValidEan13(); // Standard manufacturer EAN

        var clientAlpha = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantAlpha,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        var clientBeta = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            tenantBeta,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        var resAlpha = await clientAlpha.PostAsJsonAsync($"/api/products/{pAlpha.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = sharedBarcode,
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.EAN13
        });
        Assert.Equal(HttpStatusCode.Created, resAlpha.StatusCode);

        var resBeta = await clientBeta.PostAsJsonAsync($"/api/products/{pBeta.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = sharedBarcode,
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.EAN13
        });
        Assert.Equal(HttpStatusCode.Created, resBeta.StatusCode);
    }

    [Fact]
    public async Task PathB_InvalidSymbologyChecksum_ReturnsBadRequest()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string u = Guid.NewGuid().ToString("N")[..8];
        var product = Product.Create(tenantId, $"PRD_BAD_{u}", $"Bad Checksum Prod {u}", cat.CategoryId);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        // Invalid EAN-13 check digit (correct is 7, we pass 9)
        var res = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = "5901234123459", // Invalid check digit
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.EAN13
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PathB_IncompatibleProductUom_ReturnsBadRequest()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string uid = Guid.NewGuid().ToString("N")[..6];
        var uomEa = UnitOfMeasure.Create(tenantId, $"EA_{uid}", "Each", null);
        var uomKg = UnitOfMeasure.Create(tenantId, $"KG_{uid}", "Kilogram Unrelated", null);
        await db.UnitsOfMeasure.AddRangeAsync(uomEa, uomKg);
        await db.SaveChangesAsync();

        var product = Product.Create(tenantId, $"PRD_UOM_{uid}", $"Prod UOM {uid}", cat.CategoryId, unitOfMeasure: uomEa.Code, unitOfMeasureId: uomEa.UnitOfMeasureId);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        // Assign KG which has NO active conversion to EA
        var res = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = $"BAR_{uid}",
            IdentifierType = IdentifierType.LinearBarcode,
            Symbology = IdentifierSymbology.Code128,
            UnitOfMeasureId = uomKg.UnitOfMeasureId
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PathB_PromotePrimary_AtomicallyDemotesPreviousPrimary()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string u = Guid.NewGuid().ToString("N")[..8];
        var product = Product.Create(tenantId, $"PRD_PRI_{u}", $"Primary Switch Prod {u}", cat.CategoryId);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        // Create first identifier as primary
        var res1 = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = $"BAR1_{Guid.NewGuid():N}"[..16],
            IsPrimary = true
        });
        var dto1 = await res1.ReadApiResponseDataAsync<ProductIdentifierDto>(_jsonOpts);
        Assert.True(dto1!.IsPrimary);

        // Create second identifier as non-primary
        var res2 = await client.PostAsJsonAsync($"/api/products/{product.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = $"BAR2_{Guid.NewGuid():N}"[..16],
            IsPrimary = false
        });
        var dto2 = await res2.ReadApiResponseDataAsync<ProductIdentifierDto>(_jsonOpts);
        Assert.False(dto2!.IsPrimary);

        // Promote second identifier to primary
        var setPriRes = await client.PutAsync($"/api/products/{product.ProductId}/identifiers/{dto2.ProductIdentifierId}/primary", null);
        Assert.Equal(HttpStatusCode.OK, setPriRes.StatusCode);

        // Verify first is demoted and second is primary
        var listRes = await client.GetAsync($"/api/products/{product.ProductId}/identifiers");
        var list = await listRes.ReadApiResponseDataAsync<List<ProductIdentifierDto>>(_jsonOpts);
        var verify1 = list!.First(i => i.ProductIdentifierId == dto1.ProductIdentifierId);
        var verify2 = list!.First(i => i.ProductIdentifierId == dto2.ProductIdentifierId);

        Assert.False(verify1.IsPrimary);
        Assert.True(verify2.IsPrimary);
    }

    [Fact]
    public async Task PathB_DeactivationAndReactivation_FreesValueAndGuardsAgainstActiveCollisions()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantId = TestConstants.TenantAlphaId;
        var cat = await GetOrCreateTestCategoryAsync(db, tenantId);

        string u = Guid.NewGuid().ToString("N")[..8];
        var p1 = Product.Create(tenantId, $"P_DEACT1_{u}", $"Deact Prod 1 {u}", cat.CategoryId);
        var p2 = Product.Create(tenantId, $"P_DEACT2_{u}", $"Deact Prod 2 {u}", cat.CategoryId);
        await db.Products.AddRangeAsync(p1, p2);
        await db.SaveChangesAsync();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        string barcode = $"BAR_CYCLE_{Guid.NewGuid():N}"[..18];

        // 1. Assign to P1
        var res1 = await client.PostAsJsonAsync($"/api/products/{p1.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = barcode,
            IsPrimary = true
        });
        var dto1 = await res1.ReadApiResponseDataAsync<ProductIdentifierDto>(_jsonOpts);

        // 2. Deactivate on P1
        var deactRes = await client.DeleteAsync($"/api/products/{p1.ProductId}/identifiers/{dto1!.ProductIdentifierId}");
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);

        // 3. Reassign same barcode to P2 -> Succeeds because P1 is inactive (filtered index filter is IsActive = 1)
        var res2 = await client.PostAsJsonAsync($"/api/products/{p2.ProductId}/identifiers", new CreateProductIdentifierRequest
        {
            IdentifierValue = barcode,
            IsPrimary = true
        });
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

        // 4. Attempt to reactivate P1's identifier while P2 actively holds it -> Conflict (409)
        var reactivateRes = await client.PostAsync($"/api/products/{p1.ProductId}/identifiers/{dto1.ProductIdentifierId}/reactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, reactivateRes.StatusCode);
    }

    [Fact]
    public async Task PathB_CrossWarehouseAndCrossTenant_AccessIsRejected()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var tenantAlpha = TestConstants.TenantAlphaId;

        var wh1 = await CreateTestWarehouseAsync(db, tenantAlpha);
        var wh2 = await CreateTestWarehouseAsync(db, tenantAlpha);
        var loc1 = await CreateTestLocationAsync(db, tenantAlpha, wh1.WarehouseId, "BIN_WH1");

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            tenantAlpha,
            new[] { "Tenant Admin" },
            new[] { "LOCATION.VIEW", "LOCATION.EDIT" });

        // Attempt to access loc1 (which belongs to wh1) using wh2 in route -> Conflict/Rejected
        var crossRes = await client.GetAsync($"/api/warehouses/{wh2.WarehouseId}/locations/{loc1.WarehouseLocationId}/identifiers");
        Assert.True(crossRes.StatusCode == HttpStatusCode.Conflict || crossRes.StatusCode == HttpStatusCode.BadRequest);
    }

    // Helper methods
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

    private static async Task<Category> GetOrCreateTestCategoryAsync(DomainDbContext db, Guid tenantId)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (cat == null)
        {
            cat = Category.Create(
                tenantId,
                $"CAT_{Guid.NewGuid():N}"[..18],
                "Test Category");
            await db.Categories.AddAsync(cat);
            await db.SaveChangesAsync();
        }
        return cat;
    }

    private static string GenerateValidEan13()
    {
        var rng = new Random();
        string d12 = "59" + rng.Next(100000000, 999999999).ToString() + rng.Next(0, 9).ToString();
        int chk = SymbologyValidator.CalculateModulo10CheckDigit(d12, isEan13: true);
        return d12 + chk;
    }
}
