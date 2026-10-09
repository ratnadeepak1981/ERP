using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Procurement.PurchaseOrder;
using Xunit;

namespace ERP.SecurityTests;

public class PurchaseOrderScopeTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public PurchaseOrderScopeTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreatePurchaseOrder_AuthorizedBranchUser_Succeeds()
    {
        // Arrange: User has Company A, Branch A1 scope + CREATE & VIEW permissions
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.VIEW" });

        var uniqueOrderNum = $"PO-{Guid.NewGuid():N}"[..15];
        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 10,
                    UnitPrice = 25.50m
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(created);
        Assert.Equal(uniqueOrderNum, created.OrderNumber);
        Assert.Equal(255.00m, created.TotalAmount);
    }

    [Fact]
    public async Task CreatePurchaseOrder_WithoutCreatePermission_ReturnsForbidden()
    {
        // Arrange: User has valid scope (Company A, Branch A1), but ONLY VIEW permission (No CREATE)
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.ReadOnlyUserId,
            "readonly_user",
            TestConstants.TenantAlphaId,
            new[] { "Viewer" },
            new[] { "PURCHASE_ORDER.VIEW" });

        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = "PO-NO-PERM",
            OrderDate = DateTime.UtcNow
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            request);

        // Assert: Denied by RBAC despite having valid data scope
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPurchaseOrders_UnassignedCompany_ReturnsForbidden()
    {
        // Arrange: User has Company A, Branch A1 scope, but tries to query Company B
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW" });

        // Act
        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders");

        // Assert: Denied by Scope despite having PURCHASE_ORDER.VIEW permission
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_DetailInheritsParentScope_ForeignOrder_ReturnsNotFound()
    {
        // Arrange: User with Company A / Branch A1 scope attempts to append line item
        // to a non-existent or foreign order ID in that branch
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.EDIT" });

        var fakeOrderId = Guid.NewGuid();
        var itemRequest = new CreatePurchaseOrderItemRequest
        {
            ProductId = TestConstants.ProductAlphaId,
            Quantity = 5,
            UnitPrice = 10m
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{fakeOrderId}/items",
            itemRequest);

        // Assert: Detail mutation fails closed when parent order is not found in authorized scope (404 or 403)
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected NotFound or Forbidden, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreatePurchaseOrder_WithForeignTenantProduct_ReturnsConflict()
    {
        // Arrange: User tries to add Tenant Beta's product to Tenant Alpha's Purchase Order
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = "PO-CROSS-PROD",
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductBetaId, // Belongs to Tenant Beta!
                    Quantity = 1,
                    UnitPrice = 100m
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            request);

        // Assert: Cross-tenant product reference rejected
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreatePurchaseOrder_ForgedTenantBTarget_ForbiddenAndNoOrderCreatedInTenantB()
    {
        // Arrange: User authorized only for Tenant Alpha, Company A, Branch A1
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var forgedOrderNumber = $"PO-FORGE-{Guid.NewGuid():N}"[..16];
        var forgedRequest = new CreatePurchaseOrderRequest
        {
            OrderNumber = forgedOrderNumber,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductBetaId,
                    Quantity = 1,
                    UnitPrice = 50m
                }
            }
        };

        // Act: Attempt to forge access to Tenant Beta's Company C / Branch C1
        var response = await clientA.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyCId}/branches/{TestConstants.BranchC1Id}/purchase-orders",
            forgedRequest);

        // Assert: Request is blocked by scope authorization
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected Forbidden or NotFound, got {response.StatusCode}");

        // Verify: Tenant Beta Admin checks Company C / Branch C1 and confirms forged order was NOT created
        var clientBetaAdmin = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PURCHASE_ORDER.VIEW" });

        var betaOrdersResponse = await clientBetaAdmin.GetAsync(
            $"/api/companies/{TestConstants.CompanyCId}/branches/{TestConstants.BranchC1Id}/purchase-orders");

        Assert.Equal(HttpStatusCode.OK, betaOrdersResponse.StatusCode);

        var betaOrders = await betaOrdersResponse.Content.ReadFromJsonAsync<List<PurchaseOrderDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(betaOrders);
        Assert.DoesNotContain(betaOrders, o => o.OrderNumber == forgedOrderNumber);
    }

    [Fact]
    public async Task CreatePurchaseOrder_FailedItemValidation_LeavesNoPartiallyPersistedOrder()
    {
        // Arrange: Branch user attempts to create order with 1 valid item and 1 invalid cross-tenant item
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var orderNumber = $"PO-FAIL-{Guid.NewGuid():N}"[..16];
        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = orderNumber,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId, // Valid item
                    Quantity = 5,
                    UnitPrice = 20m
                },
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductBetaId, // Invalid item: foreign tenant
                    Quantity = 1,
                    UnitPrice = 100m
                }
            }
        };

        // Act: Create fails on cross-tenant product check
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Assert: Verify order was NOT partially persisted in database
        var listResponse = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var orders = await listResponse.Content.ReadFromJsonAsync<List<PurchaseOrderDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(orders);
        Assert.DoesNotContain(orders, o => o.OrderNumber == orderNumber);
    }

    [Fact]
    public async Task CreatePurchaseOrder_FinancialRounding_CalculatesAwayFromZeroConsistentAmounts()
    {
        // Arrange: Valid branch user creates order with fractional monetary and quantity amounts
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var uniqueOrderNum = $"PO-RND-{Guid.NewGuid():N}"[..16];
        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId,
                    // Quantity: 3.3333, UnitPrice: 10.55m -> LineTotal: 35.17m
                    Quantity = 3.3333m,
                    UnitPrice = 10.55m
                },
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId,
                    // Quantity: 2.5000, UnitPrice: 4.125m -> UnitPrice: 4.13m, LineTotal: 10.33m
                    Quantity = 2.5000m,
                    UnitPrice = 4.125m
                }
            }
        };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(created);
        // TotalAmount: 35.17 + 10.33 = 45.50m
        Assert.Equal(45.50m, created.TotalAmount);
    }

    [Fact]
    public void PurchaseOrder_StatusTransitions_EnforceDomainRules()
    {
        // Domain Rule Test: Status guards and transitions
        var order = PurchaseOrder.Create(
            TestConstants.TenantAlphaId,
            TestConstants.CompanyAId,
            TestConstants.BranchA1Id,
            "PO-STATUS-001");

        // 1. Cannot submit empty order
        Assert.Throws<InvalidOperationException>(() => order.Submit());

        // 2. Add item in Draft status succeeds
        order.AddItem(TestConstants.ProductAlphaId, 2, 50m);
        Assert.Equal(100m, order.TotalAmount);

        // 3. Submit succeeds
        order.Submit();
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);

        // 4. Cannot modify items on Submitted order
        var ex = Assert.Throws<InvalidOperationException>(() =>
            order.AddItem(TestConstants.ProductAlphaId, 1, 20m));
        Assert.Contains("Only Draft orders can be modified", ex.Message);

        // 5. Approve order
        order.Approve();
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
    }

    [Fact]
    public async Task CreatePurchaseOrder_InCompanyB_UsingTenantOwnedProductFromCompanyA_Succeeds()
    {
        // Scenario 3: A tenant-owned Product created/visible in Company A is available to an authorized
        // user creating a Purchase Order in Company B within the same tenant.
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MixedScopeUserId,
            "mixedscope_user",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var uniqueOrderNum = $"PO-CO-B-{Guid.NewGuid():N}"[..16];
        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId, // Master product belonging to Tenant Alpha
                    Quantity = 5,
                    UnitPrice = 30m
                }
            }
        };

        // Act: Create order in Company B, Branch B2 using Tenant Alpha product
        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB2Id}/purchase-orders",
            request);

        // Assert: Succeeds because Product master table is shared across companies within Tenant Alpha
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(created);
        Assert.Equal(uniqueOrderNum, created.OrderNumber);
        Assert.Equal(150m, created.TotalAmount);
    }

    [Fact]
    public async Task CreateOrAccessPurchaseOrder_CompanyAUserTargetingCompanyB_ReturnsForbidden()
    {
        // Scenario 5: A user with access only to Company A cannot create or access a Purchase Order in Company B
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var request = new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-DENIED-{Guid.NewGuid():N}"[..16],
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 1,
                    UnitPrice = 10m
                }
            }
        };

        // 1. Creation attempt in Company B -> Forbidden
        var createResponse = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders",
            request);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        // 2. Listing attempt in Company B -> Forbidden
        var listResponse = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        // 3. Direct order lookup in Company B -> Forbidden
        var getResponse = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreatePurchaseOrder_MultiCompanyUser_EnforcesBranchRestrictionsWithinEachCompany()
    {
        // Scenario 6: Verify branch restrictions still apply when a user has access to multiple companies
        // MixedScopeUser is authorized for:
        // - (Company A, Branch A1) -> Allowed
        // - (Company B, Branch B2) -> Allowed
        // But NOT authorized for:
        // - (Company A, Branch A2) -> Denied
        // - (Company B, Branch B1) -> Denied
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MixedScopeUserId,
            "mixedscope_user",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        var item = new List<CreatePurchaseOrderItemRequest>
        {
            new CreatePurchaseOrderItemRequest
            {
                ProductId = TestConstants.ProductAlphaId,
                Quantity = 1,
                UnitPrice = 10m
            }
        };

        // 1. Attempt in Company A, Branch A2 (unauthorized branch in authorized company) -> Denied
        var resA2 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA2Id}/purchase-orders",
            new CreatePurchaseOrderRequest { OrderNumber = $"PO-A2-{Guid.NewGuid():N}"[..14], Items = item });
        Assert.Equal(HttpStatusCode.Forbidden, resA2.StatusCode);

        // 2. Attempt in Company B, Branch B1 (unauthorized branch in authorized company) -> Denied
        var resB1 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders",
            new CreatePurchaseOrderRequest { OrderNumber = $"PO-B1-{Guid.NewGuid():N}"[..14], Items = item });
        Assert.Equal(HttpStatusCode.Forbidden, resB1.StatusCode);

        // 3. Attempt in Company A, Branch A1 (authorized pairing) -> Allowed
        var resA1 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            new CreatePurchaseOrderRequest { OrderNumber = $"PO-A1-{Guid.NewGuid():N}"[..14], Items = item });
        Assert.Equal(HttpStatusCode.Created, resA1.StatusCode);

        // 4. Attempt in Company B, Branch B2 (authorized pairing) -> Allowed
        var resB2 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB2Id}/purchase-orders",
            new CreatePurchaseOrderRequest { OrderNumber = $"PO-B2-{Guid.NewGuid():N}"[..14], Items = item });
        Assert.Equal(HttpStatusCode.Created, resB2.StatusCode);
    }

    [Fact]
    public async Task DirectApiInvocation_BypassingUi_EnforcesAuthorization()
    {
        // Scenario 7: Verify authorization is enforced by the API and not merely by UI filtering
        // Client issues direct crafted HTTP requests targeting forbidden company and branch IDs
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "COMPANY.VIEW", "BRANCH.VIEW" });

        // 1. Direct GET against forbidden company ID bypassing any UI dropdown
        var directCompRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}");
        Assert.True(directCompRes.StatusCode == HttpStatusCode.Forbidden || directCompRes.StatusCode == HttpStatusCode.NotFound);

        // 2. Direct GET against forbidden branch ID
        var directBranchRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}");
        Assert.True(directBranchRes.StatusCode == HttpStatusCode.Forbidden || directBranchRes.StatusCode == HttpStatusCode.NotFound);

        // 3. Direct POST with JSON payload targeting foreign company/branch
        var rawPayload = new CreatePurchaseOrderRequest
        {
            OrderNumber = $"PO-DIRECT-{Guid.NewGuid():N}"[..16],
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new CreatePurchaseOrderItemRequest { ProductId = TestConstants.ProductAlphaId, Quantity = 1, UnitPrice = 10m }
            }
        };
        var directPostRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders",
            rawPayload);
        Assert.Equal(HttpStatusCode.Forbidden, directPostRes.StatusCode);

        // 4. Direct GET against cross-tenant resource (Tenant Beta's Company C)
        var crossTenantRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyCId}");
        Assert.True(crossTenantRes.StatusCode == HttpStatusCode.Forbidden || crossTenantRes.StatusCode == HttpStatusCode.NotFound);
    }

    private class PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}
