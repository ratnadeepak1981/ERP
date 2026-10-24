using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class InventoryPutawayPermissionDecouplingTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public InventoryPutawayPermissionDecouplingTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private System.Net.Http.HttpClient CreateTenantClient(IEnumerable<string> permissions, Guid? userId = null)
    {
        return _fixture.CreateAuthenticatedClient(
            userId: userId ?? TestConstants.TenantUserAlphaId,
            username: "tenantuser_alpha",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "Tenant User" },
            permissions: permissions);
    }

    #region 1. Inventory Summary & Read Decoupling Tests

    [Fact]
    public async Task GetInventorySummary_UserWithProductViewOnly_ReturnsForbidden()
    {
        // User has Product Master view permission, but lacks INVENTORY.VIEW
        var client = CreateTenantClient(new[] { "PRODUCT.VIEW" });
        var response = await client.GetAsync("/api/inventory/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInventorySummary_UserWithInventoryView_ReturnsOkWithoutProductPermissions()
    {
        // User has INVENTORY.VIEW permission, but zero product permissions
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW" });
        var response = await client.GetAsync("/api/inventory/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRecentTransactions_UserWithProductViewOnly_ReturnsForbidden()
    {
        var client = CreateTenantClient(new[] { "PRODUCT.VIEW" });
        var response = await client.GetAsync("/api/inventory/recent-transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRecentTransactions_UserWithInventoryView_ReturnsOkWithoutProductPermissions()
    {
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW" });
        var response = await client.GetAsync("/api/inventory/recent-transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region 2. Stock Receipt & Issue Transaction Decoupling Tests

    [Fact]
    public async Task CreateStockReceipt_UserWithProductEditOnly_ReturnsForbidden()
    {
        // User has PRODUCT.VIEW and PRODUCT.EDIT, but no INVENTORY.VIEW or STOCK_RECEIPT.POST
        var client = CreateTenantClient(new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-receipts", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 10,
            unitCost = 15.0m
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStockReceipt_UserWithInventoryViewOnly_ReturnsForbidden()
    {
        // User has INVENTORY.VIEW, but missing STOCK_RECEIPT.POST
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-receipts", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 10,
            unitCost = 15.0m
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStockReceipt_UserWithStockReceiptPostAndInventoryView_PassesAuthorizationWithoutProductPermissions()
    {
        // User has INVENTORY.VIEW and STOCK_RECEIPT.POST, but NO PRODUCT.VIEW or PRODUCT.EDIT
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW", "STOCK_RECEIPT.POST" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-receipts", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 10,
            unitCost = 15.0m
        });

        // Response should NOT be Forbidden (it will reach domain service and may return BadRequest or OK, not 403)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStockIssue_UserWithProductEditOnly_ReturnsForbidden()
    {
        // User has PRODUCT.VIEW and PRODUCT.EDIT, but no INVENTORY.VIEW or STOCK_ISSUE.POST
        var client = CreateTenantClient(new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-issues", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStockIssue_UserWithInventoryViewOnly_ReturnsForbidden()
    {
        // User has INVENTORY.VIEW, but missing STOCK_ISSUE.POST
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-issues", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStockIssue_UserWithStockIssuePostAndInventoryView_PassesAuthorizationWithoutProductPermissions()
    {
        // User has INVENTORY.VIEW and STOCK_ISSUE.POST, but NO PRODUCT.VIEW or PRODUCT.EDIT
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW", "STOCK_ISSUE.POST" });
        var response = await client.PostAsJsonAsync("/api/inventory/stock-issues", new
        {
            warehouseLocationId = Guid.NewGuid(),
            productId = TestConstants.ProductAlphaId,
            quantity = 5
        });

        // Response should NOT be Forbidden (it reaches the controller method)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region 3. Putaway Operation Decoupling Tests

    [Fact]
    public async Task GetPutaways_UserWithProductViewOnly_ReturnsForbidden()
    {
        // User has PRODUCT.VIEW, but lacks INVENTORY.VIEW
        var client = CreateTenantClient(
            new[] { "PRODUCT.VIEW" },
            userId: TestConstants.CompanyUserAId); // User scoped to Company A

        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPutaways_UserWithInventoryView_ReturnsOkWithoutProductPermissions()
    {
        // User has INVENTORY.VIEW, but NO PRODUCT.VIEW
        var client = CreateTenantClient(
            new[] { "INVENTORY.VIEW" },
            userId: TestConstants.CompanyUserAId); // User scoped to Company A

        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreatePutawayDraft_UserWithProductEditOnly_ReturnsForbidden()
    {
        // User has PRODUCT.VIEW & PRODUCT.EDIT, but lacks PUTAWAY.POST & INVENTORY.VIEW
        var client = CreateTenantClient(
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" },
            userId: TestConstants.CompanyUserAId);

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways",
            new
            {
                sourceDocumentType = "GoodsReceipt",
                sourceDocumentId = Guid.NewGuid(),
                lines = new object[] { }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePutawayDraft_UserWithInventoryViewOnly_ReturnsForbidden()
    {
        // User has INVENTORY.VIEW, but lacks PUTAWAY.POST
        var client = CreateTenantClient(
            new[] { "INVENTORY.VIEW" },
            userId: TestConstants.CompanyUserAId);

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways",
            new
            {
                sourceDocumentType = "GoodsReceipt",
                sourceDocumentId = Guid.NewGuid(),
                lines = new object[] { }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmPutaway_UserWithProductEditOnly_ReturnsForbidden()
    {
        // User has PRODUCT.VIEW & PRODUCT.EDIT, but lacks PUTAWAY.POST & INVENTORY.VIEW
        var client = CreateTenantClient(
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" },
            userId: TestConstants.CompanyUserAId);

        var response = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways/{Guid.NewGuid()}/confirm",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmPutaway_UserWithPutawayPostAndInventoryView_PassesAuthorizationWithoutProductPermissions()
    {
        // User has INVENTORY.VIEW & PUTAWAY.POST, but NO PRODUCT.VIEW or PRODUCT.EDIT
        var client = CreateTenantClient(
            new[] { "INVENTORY.VIEW", "PUTAWAY.POST" },
            userId: TestConstants.CompanyUserAId);

        var response = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/putaways/{Guid.NewGuid()}/confirm",
            null);

        // Reaches endpoint logic (returns 404 Not Found since fake ID, proving 403 Forbidden was not triggered)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region 4. Product Master Independence Tests

    [Fact]
    public async Task GetProducts_UserWithInventoryAndPutawayPermissionsOnly_ReturnsForbidden()
    {
        // User has full inventory and putaway permissions, but NO PRODUCT.VIEW
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW", "STOCK_RECEIPT.POST", "STOCK_ISSUE.POST", "PUTAWAY.POST" });
        var response = await client.GetAsync("/api/products");

        // Must still be protected by PRODUCT.VIEW
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_UserWithInventoryAndPutawayPermissionsOnly_ReturnsForbidden()
    {
        // User has full inventory and putaway permissions, but NO PRODUCT.CREATE
        var client = CreateTenantClient(new[] { "INVENTORY.VIEW", "STOCK_RECEIPT.POST", "STOCK_ISSUE.POST", "PUTAWAY.POST" });
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            productCode = "PROD-TEST",
            productName = "Decouple Test Product",
            unitOfMeasure = "PCS"
        });

        // Must still be protected by PRODUCT.CREATE & PRODUCT.VIEW
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_UserWithProductView_Succeeds()
    {
        // Product master operations continue to work as expected with PRODUCT.VIEW
        var client = CreateTenantClient(new[] { "PRODUCT.VIEW" });
        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion
}
