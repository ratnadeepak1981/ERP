using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using API.Contracts.Pagination;
using Domain.Features.MasterData.Product;
using Xunit;

namespace ERP.SecurityTests;

public record ProductDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal SellingPrice,
    decimal StandardCost,
    bool IsActive);

[Collection("SecurityTestCollection")]
public class ApiContractEndpointIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] FullPermissions = new[]
    {
        "PRODUCT.VIEW", "PRODUCT.CREATE", "PRODUCT.EDIT", "PRODUCT.UPDATE", "PRODUCT.DELETE",
        "WAREHOUSE.VIEW", "WAREHOUSE.CREATE",
        "TENANT.CREATE"
    };

    public ApiContractEndpointIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private HttpClient CreateAdminClient()
    {
        return _fixture.CreateAuthenticatedClient(
            userId: TestConstants.TenantAdminAlphaId,
            username: "TenantAdminAlpha",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "TenantAdmin" },
            permissions: FullPermissions);
    }

    #region 1. Standard Success Reads & Lists (200 OK)

    [Fact]
    public async Task GetProducts_Returns200_WithCompliantApiResponseEnvelope()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType.MediaType);

        var apiResponse = await response.ReadApiResponseAsync<IReadOnlyList<ProductDto>>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Succeeded);
        Assert.NotNull(apiResponse.Data);
        Assert.Null(apiResponse.ErrorCode);
        Assert.Empty(apiResponse.Errors);
        Assert.False(string.IsNullOrWhiteSpace(apiResponse.Message));
    }

    #endregion

    #region 2. Pagination Contract & Metadata (200 OK)

    [Fact]
    public async Task GetProducts_WithPaging_Returns200_WithCompliantPagedResponseEnvelope()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/products?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var apiResponse = await response.ReadApiResponseAsync<PagedResponse<ProductDto>>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Succeeded);
        Assert.NotNull(apiResponse.Data);

        var paged = apiResponse.Data;
        Assert.Equal(1, paged.Page);
        Assert.Equal(5, paged.PageSize);
        Assert.True(paged.TotalCount >= 0);
        Assert.True(paged.TotalPages >= 0);
        Assert.False(paged.HasPreviousPage);
        Assert.NotNull(paged.Items);
    }

    #endregion

    #region 3. Resource Creation Contract (201 Created)

    [Fact]
    public async Task CreateProduct_Returns201Created_WithEnvelopeAndLocationHeader()
    {
        var client = CreateAdminClient();
        string pCode = $"PRD_CONTRACT_{Guid.NewGuid():N}"[..18];

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = pCode,
            ProductName = $"Contract Test Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 99.99m,
            StandardCost = 49.99m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var apiResponse = await response.ReadApiResponseAsync<ProductDto>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.True(apiResponse.Succeeded);
        Assert.NotNull(apiResponse.Data);
        Assert.Equal(pCode, apiResponse.Data.ProductCode);
        Assert.NotEqual(Guid.Empty, apiResponse.Data.ProductId);
        Assert.Null(apiResponse.ErrorCode);
        Assert.Empty(apiResponse.Errors);
    }

    #endregion

    #region 4. Resource Update Contract (200 OK)

    [Fact]
    public async Task UpdateProduct_Returns200OK_WithEnvelope()
    {
        var client = CreateAdminClient();
        string pCode = $"PRD_UPD_{Guid.NewGuid():N}"[..18];

        var createRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = pCode,
            ProductName = $"Update Target Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 100m,
            StandardCost = 50m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.ReadApiResponseDataAsync<ProductDto>(_jsonOpts);

        var updateRes = await client.PutAsJsonAsync($"/api/products/{created!.ProductId}", new UpdateProductRequest
        {
            ProductName = $"Updated Target Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 125m,
            StandardCost = 60m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true
        });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.ReadApiResponseDataAsync<ProductDto>(_jsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(125m, updated.SellingPrice);
        Assert.Equal(60m, updated.StandardCost);
    }

    #endregion

    #region 5. Intentional 204 No Content Exception

    [Fact]
    public async Task DeactivateProduct_Returns204NoContent_WithoutBody()
    {
        var client = CreateAdminClient();
        string pCode = $"PRD_DEL_{Guid.NewGuid():N}"[..18];

        var createRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = pCode,
            ProductName = $"Delete Target Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 10m,
            StandardCost = 5m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.ReadApiResponseDataAsync<ProductDto>(_jsonOpts);

        // Act: Delete/Deactivate
        var deleteRes = await client.DeleteAsync($"/api/products/{created!.ProductId}");

        // Assert: 204 NoContent with empty body (RFC 9110 exception)
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        var content = await deleteRes.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrEmpty(content));
    }

    #endregion

    #region 6. Not Found Contract (404 Not Found)

    [Fact]
    public async Task GetProduct_NonExistentId_Returns404_WithFailureEnvelope()
    {
        var client = CreateAdminClient();
        var nonExistentId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/products/{nonExistentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var apiResponse = await response.ReadApiResponseAsync<object>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Null(apiResponse.Data);
        Assert.Equal("NOT_FOUND", apiResponse.ErrorCode);
        Assert.Contains("not found", apiResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 7. Business Rule Conflict Contract (409 Conflict)

    [Fact]
    public async Task CreateProduct_DuplicateCode_Returns409Conflict_WithFailureEnvelope()
    {
        var client = CreateAdminClient();
        string pCode = $"PRD_DUP_{Guid.NewGuid():N}"[..18];

        var firstRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = pCode,
            ProductName = $"Original Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 20m,
            StandardCost = 10m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);

        // Attempt to create duplicate product code
        var dupRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = pCode,
            ProductName = $"Duplicate Product {pCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 25m,
            StandardCost = 15m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });

        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        var apiResponse = await dupRes.ReadApiResponseAsync<object>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Null(apiResponse.Data);
        Assert.Equal("CONFLICT", apiResponse.ErrorCode);
    }

    #endregion

    #region 8. Validation / Bad Request Contract (400 Bad Request)

    [Fact]
    public async Task CreateProduct_BlankCode_Returns400BadRequest_WithFailureEnvelope()
    {
        var client = CreateAdminClient();

        var response = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = "   ", // Blank code
            ProductName = "Blank Code Product",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 10m,
            StandardCost = 5m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var apiResponse = await response.ReadApiResponseAsync<object>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Null(apiResponse.Data);
        Assert.Equal("BAD_REQUEST", apiResponse.ErrorCode);
        Assert.Contains("code is required", apiResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProducts_InvalidSortField_Returns400_WithInvalidSortErrorCode()
    {
        var client = CreateAdminClient();

        var response = await client.GetAsync("/api/products?sortBy=malicious_sql;DROP_TABLE--&sortDirection=asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var apiResponse = await response.ReadApiResponseAsync<object>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("INVALID_SORT", apiResponse.ErrorCode);
        Assert.Contains("invalid", apiResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 9. Authentication & Authorization Failure Contracts (401 & 403)

    [Fact]
    public async Task GetProducts_Unauthenticated_Returns401Unauthorized()
    {
        var unauthenticatedClient = _fixture.CreateClient();
        var response = await unauthenticatedClient.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_PlatformAdminToken_Returns403Forbidden()
    {
        // Platform user has null tenantId -> forbidden from accessing tenant ERP products
        var platformClient = _fixture.CreateAuthenticatedClient(
            TestConstants.PlatformAdminId,
            "platformadmin",
            tenantId: null,
            roles: new[] { "Platform Admin" },
            permissions: new[] { "PRODUCT.VIEW" });

        var response = await platformClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region 10. Unexpected Server Error Traceability Contract (500)

    [Fact]
    public async Task ServerError_Returns500_WithTraceId_AndNoInformationLeak()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/security/error-simulation-test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        string rawContent = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack", rawContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", rawContent);
        Assert.DoesNotContain("Simulated unexpected", rawContent);

        var apiResponse = await response.ReadApiResponseAsync<object>(_jsonOpts);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Succeeded);
        Assert.Equal("INTERNAL_SERVER_ERROR", apiResponse.ErrorCode);
        Assert.Equal("An unexpected error occurred while processing your request.", apiResponse.Message);
        Assert.False(string.IsNullOrWhiteSpace(apiResponse.TraceId));
    }

    #endregion
}
