using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Brand;
using Domain.Features.MasterData.Product;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class ProductBrandIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public ProductBrandIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateProduct_WithValidBrand_PersistsBrandIdAndReturns201()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE", "BRAND.VIEW", "BRAND.CREATE" });

        // 1. Create a brand in Tenant Alpha
        var brandCode = $"BR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCode,
            BrandName = $"Brand {brandCode}",
            Description = "Brand for product test"
        });
        Assert.Equal(HttpStatusCode.Created, brandRes.StatusCode);
        var brandJson = await brandRes.ReadDataJsonElementAsync();
        var brandId = brandJson.GetProperty("brandId").GetGuid();

        // 2. Create a product with this BrandId
        var prodCode = $"PRD_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var prodRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = prodCode,
            ProductName = $"Product {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = brandId
        });

        Assert.Equal(HttpStatusCode.Created, prodRes.StatusCode);
        var prodJson = await prodRes.ReadDataJsonElementAsync();
        var prodId = prodJson.GetProperty("productId").GetGuid();
        Assert.Equal(brandId, prodJson.GetProperty("brandId").GetGuid());

        // 3. Verify get by ID returns the brandId
        var getRes = await client.GetAsync($"/api/products/{prodId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getJson = await getRes.ReadDataJsonElementAsync();
        Assert.Equal(brandId, getJson.GetProperty("brandId").GetGuid());
    }

    [Fact]
    public async Task CreateProduct_WithoutBrand_SucceedsWithNullBrandId()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE" });

        var prodCode = $"PRD_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var prodRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = prodCode,
            ProductName = $"Unbranded {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = null
        });

        Assert.Equal(HttpStatusCode.Created, prodRes.StatusCode);
        var prodJson = await prodRes.ReadDataJsonElementAsync();
        var prodId = prodJson.GetProperty("productId").GetGuid();
        Assert.Equal(JsonValueKind.Null, prodJson.GetProperty("brandId").ValueKind);

        // Verify get by ID returns null BrandId
        var getRes = await client.GetAsync($"/api/products/{prodId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var getJson = await getRes.ReadDataJsonElementAsync();
        Assert.Equal(JsonValueKind.Null, getJson.GetProperty("brandId").ValueKind);
    }

    [Fact]
    public async Task UpdateProduct_WithValidBrand_UpdatesBrandIdSuccessfully()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE", "PRODUCT.EDIT", "BRAND.VIEW", "BRAND.CREATE" });

        // 1. Create a brand
        var brandCode = $"BR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCode,
            BrandName = $"Brand {brandCode}"
        });
        var brandId = (await brandRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        // 2. Create product without brand
        var prodCode = $"PRD_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var prodRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = prodCode,
            ProductName = $"Product {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS"
        });
        var prodId = (await prodRes.ReadDataJsonElementAsync()).GetProperty("productId").GetGuid();

        // 3. Update product to set brand
        var updateRes = await client.PutAsJsonAsync($"/api/products/{prodId}", new UpdateProductRequest
        {
            ProductName = $"Product Updated {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = brandId
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedJson = await updateRes.ReadDataJsonElementAsync();
        Assert.Equal(brandId, updatedJson.GetProperty("brandId").GetGuid());

        // 4. Update product to remove brand (set back to null)
        var removeRes = await client.PutAsJsonAsync($"/api/products/{prodId}", new UpdateProductRequest
        {
            ProductName = $"Product Unbranded {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = null
        });
        Assert.Equal(HttpStatusCode.OK, removeRes.StatusCode);
        var removedJson = await removeRes.ReadDataJsonElementAsync();
        Assert.Equal(JsonValueKind.Null, removedJson.GetProperty("brandId").ValueKind);
    }

    [Fact]
    public async Task CreateProduct_WithCrossTenantBrand_RejectsWithConflict409()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW", "BRAND.CREATE" });

        // 1. Create a brand in Tenant Beta
        var brandCode = $"BR_B_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes = await clientB.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCode,
            BrandName = $"Tenant Beta Brand {brandCode}"
        });
        Assert.Equal(HttpStatusCode.Created, brandRes.StatusCode);
        var betaBrandId = (await brandRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        // 2. Tenant Alpha tries to create product referencing Tenant Beta's brand
        var prodRes = await clientA.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = $"PRD_CROSS_{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ProductName = "Cross-Tenant Brand Product",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = betaBrandId
        });

        Assert.Equal(HttpStatusCode.Conflict, prodRes.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithInactiveBrand_RejectsWithConflict409()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE", "BRAND.VIEW", "BRAND.CREATE", "BRAND.DELETE" });

        // 1. Create brand
        var brandCode = $"BI_{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var brandRes = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCode,
            BrandName = $"Inactive Brand {brandCode}"
        });
        Assert.Equal(HttpStatusCode.Created, brandRes.StatusCode);
        var brandId = (await brandRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        // 2. Deactivate brand
        var deactRes = await client.DeleteAsync($"/api/brands/{brandId}");
        Assert.Equal(HttpStatusCode.NoContent, deactRes.StatusCode);

        // 3. Try to create product with deactivated brand
        var prodRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = $"PRD_FAIL_{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ProductName = "Inactive Brand Product",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = brandId
        });

        Assert.Equal(HttpStatusCode.Conflict, prodRes.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithNonexistentBrand_RejectsWithConflict409()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE" });

        var prodRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = $"PRD_NON_{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            ProductName = "Nonexistent Brand Product",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Conflict, prodRes.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithCrossTenantBrand_RejectsWithConflict409()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE", "PRODUCT.EDIT" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW", "BRAND.CREATE" });

        // 1. Create product in Tenant Alpha
        var prodCode = $"PRD_A_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var prodRes = await clientA.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = prodCode,
            ProductName = $"Product {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS"
        });
        var prodId = (await prodRes.ReadDataJsonElementAsync()).GetProperty("productId").GetGuid();

        // 2. Create brand in Tenant Beta
        var brandCode = $"BR_B2_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes = await clientB.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCode,
            BrandName = $"Beta Brand {brandCode}"
        });
        var betaBrandId = (await brandRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        // 3. Tenant Alpha tries to update product referencing Tenant Beta's brand
        var updateRes = await clientA.PutAsJsonAsync($"/api/products/{prodId}", new UpdateProductRequest
        {
            ProductName = $"Product {prodCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = betaBrandId
        });

        Assert.Equal(HttpStatusCode.Conflict, updateRes.StatusCode);
    }

    [Fact]
    public async Task GetProducts_FilterByBrandId_ReturnsOnlyProductsForThatBrand()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE", "BRAND.VIEW", "BRAND.CREATE" });

        // 1. Create two distinct brands
        var brandCodeX = $"BR_X_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes1 = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCodeX,
            BrandName = $"Brand X {brandCodeX}"
        });
        Assert.Equal(HttpStatusCode.Created, brandRes1.StatusCode);
        var brandIdX = (await brandRes1.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        var brandCodeY = $"BR_Y_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var brandRes2 = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = brandCodeY,
            BrandName = $"Brand Y {brandCodeY}"
        });
        Assert.Equal(HttpStatusCode.Created, brandRes2.StatusCode);
        var brandIdY = (await brandRes2.ReadDataJsonElementAsync()).GetProperty("brandId").GetGuid();

        // 2. Create Product 1 with Brand X
        var p1Code = $"PRD_X_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var p1Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p1Code,
            ProductName = $"Product 1 ({p1Code})",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = brandIdX
        });
        var p1Id = (await p1Res.ReadDataJsonElementAsync()).GetProperty("productId").GetGuid();

        // 3. Create Product 2 with Brand Y
        var p2Code = $"PRD_Y_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var p2Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p2Code,
            ProductName = $"Product 2 ({p2Code})",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = brandIdY
        });
        var p2Id = (await p2Res.ReadDataJsonElementAsync()).GetProperty("productId").GetGuid();

        // 4. Create Product 3 without brand
        var p3Code = $"PRD_Z_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var p3Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p3Code,
            ProductName = $"Product 3 ({p3Code})",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            BrandId = null
        });
        var p3Id = (await p3Res.ReadDataJsonElementAsync()).GetProperty("productId").GetGuid();

        // 5. Query products filtered by brandId = brandIdX
        var filterRes = await client.GetAsync($"/api/products?brandId={brandIdX}");
        Assert.Equal(HttpStatusCode.OK, filterRes.StatusCode);
        var listJson = await filterRes.ReadDataJsonElementAsync();

        // Check if list is array
        var productElements = listJson.EnumerateArray().ToList();
        var productIds = productElements.Select(x => x.GetProperty("productId").GetGuid()).ToList();

        Assert.Contains(p1Id, productIds);
        Assert.DoesNotContain(p2Id, productIds);
        Assert.DoesNotContain(p3Id, productIds);

        foreach (var p in productElements)
        {
            Assert.Equal(brandIdX, p.GetProperty("brandId").GetGuid());
        }
    }
}
