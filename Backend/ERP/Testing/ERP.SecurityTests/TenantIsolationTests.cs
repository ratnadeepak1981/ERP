using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Xunit;

namespace ERP.SecurityTests;

public class TenantIsolationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public TenantIsolationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetProducts_TenantA_ReturnsOnlyTenantAProducts()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<ProductDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(products);
        Assert.Contains(products, p => p.ProductId == TestConstants.ProductAlphaId);
        Assert.DoesNotContain(products, p => p.ProductId == TestConstants.ProductBetaId);
    }

    [Fact]
    public async Task GetProducts_TenantB_ReturnsOnlyTenantBProducts()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<ProductDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(products);
        Assert.Contains(products, p => p.ProductId == TestConstants.ProductBetaId);
        Assert.DoesNotContain(products, p => p.ProductId == TestConstants.ProductAlphaId);
    }

    [Fact]
    public async Task GetProductById_TenantACrossTenantLookup_ReturnsNotFound()
    {
        // Arrange
        // Tenant A user tries to query Tenant B's Product directly by ID
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync($"/api/products/{TestConstants.ProductBetaId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_TenantBCrossTenantLookup_ReturnsNotFound()
    {
        // Arrange
        // Tenant B user tries to query Tenant A's Product directly by ID
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync($"/api/products/{TestConstants.ProductAlphaId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_DerivesTenantFromToken_CannotCreateForOtherTenant()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.CREATE", "PRODUCT.VIEW" });

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueProductCode = $"ISO_{uniqueSuffix}";
        var uniqueProductName = $"Isolation Product {uniqueSuffix}";
        var request = new CreateProductRequest
        {
            ProductCode = uniqueProductCode,
            ProductName = uniqueProductName,
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/products", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProductDto>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);
        Assert.Equal(TestConstants.TenantAlphaId, created.TenantId);

        // Now verify Tenant B cannot see this newly created product
        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        var crossResponse = await clientB.GetAsync($"/api/products/{created.ProductId}");
        Assert.Equal(HttpStatusCode.NotFound, crossResponse.StatusCode);
    }

    private class ProductDto
    {
        public System.Guid ProductId { get; set; }
        public System.Guid TenantId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
    }
}
