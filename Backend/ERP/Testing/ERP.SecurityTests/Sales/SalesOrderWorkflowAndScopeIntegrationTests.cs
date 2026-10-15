using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Sales.SalesOrder;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.SecurityTests;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesOrderWorkflowAndScopeIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesOrderWorkflowAndScopeIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateSalesOrder_AuthorizedBranchUser_Succeeds()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE", "SALES_ORDER.VIEW" });

        var uniqueOrderNum = $"SO-{Guid.NewGuid():N}"[..15];
        var request = new CreateSalesOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            CustomerId = TestConstants.CustomerAlphaId,
            OrderDate = DateTime.UtcNow,
            Currency = "USD",
            Items = new List<CreateSalesOrderItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 5,
                    UnitPrice = 100m,
                    DiscountRate = 10m,
                    TaxRate = 5m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Equal(uniqueOrderNum, created.OrderNumber);
        Assert.Equal(SalesOrderStatus.Draft, created.Status);
        // SubTotal: 500.00, Discount: 50.00, Taxable: 450.00, Tax: 22.50, Total: 472.50
        Assert.Equal(500.00m, created.SubTotal);
        Assert.Equal(50.00m, created.DiscountAmount);
        Assert.Equal(22.50m, created.TaxAmount);
        Assert.Equal(472.50m, created.TotalAmount);
    }

    [Fact]
    public async Task CreateSalesOrder_WithoutCreatePermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.ReadOnlyUserId,
            "readonly_user",
            TestConstants.TenantAlphaId,
            new[] { "Viewer" },
            new[] { "SALES_ORDER.VIEW" });

        var request = new CreateSalesOrderRequest
        {
            OrderNumber = $"SO-NOPERM-{Guid.NewGuid():N}"[..15],
            CustomerId = TestConstants.CustomerAlphaId
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSalesOrders_WithoutViewPermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE" }); // Has CREATE, lacks VIEW

        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSalesOrders_UnassignedCompany_ReturnsForbidden()
    {
        // BranchUserA1 belongs to Company A, attempts to query Company B
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.VIEW" });

        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/sales-orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSalesOrders_UnassignedBranch_ReturnsForbidden()
    {
        // BranchUserA1 belongs to Branch A1, attempts to query Branch A2
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.VIEW" });

        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA2Id}/sales-orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSalesOrder_CRMIndependent_SucceedsWithoutLeadsOrOpportunities()
    {
        // Verify Sales Order Core functions completely standalone without CRM
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE", "SALES_ORDER.VIEW" });

        var uniqueOrderNum = $"SO-NOCRM-{Guid.NewGuid():N}"[..15];
        var request = new CreateSalesOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            CustomerId = TestConstants.CustomerAlphaId,
            OrderDate = DateTime.UtcNow,
            OpportunityId = null, // No CRM association
            Items = new List<CreateSalesOrderItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 2,
                    UnitPrice = 50m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Null(created.OpportunityId);
        Assert.Equal(100.00m, created.TotalAmount);
    }

    [Fact]
    public async Task CreateSalesOrder_CrossTenantCustomer_ReturnsConflict()
    {
        // Branch user in Tenant Alpha attempts to create order for Customer belonging to Tenant Beta
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE", "SALES_ORDER.VIEW" });

        var request = new CreateSalesOrderRequest
        {
            OrderNumber = $"SO-XTCUST-{Guid.NewGuid():N}"[..15],
            CustomerId = TestConstants.CustomerBetaId, // Tenant Beta customer!
            Items = new List<CreateSalesOrderItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 1, UnitPrice = 10m }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSalesOrder_CrossTenantProduct_ReturnsConflict()
    {
        // Branch user in Tenant Alpha attempts to add Product belonging to Tenant Beta
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE", "SALES_ORDER.VIEW" });

        var request = new CreateSalesOrderRequest
        {
            OrderNumber = $"SO-XTPROD-{Guid.NewGuid():N}"[..15],
            CustomerId = TestConstants.CustomerAlphaId,
            Items = new List<CreateSalesOrderItemRequest>
            {
                new() { ProductId = TestConstants.ProductBetaId, Quantity = 1, UnitPrice = 10m } // Tenant Beta product!
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSalesOrder_DuplicateOrderNumber_ReturnsConflict()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.CREATE", "SALES_ORDER.VIEW" });

        var duplicateNum = $"SO-DUP-{Guid.NewGuid():N}"[..15];
        var request = new CreateSalesOrderRequest
        {
            OrderNumber = duplicateNum,
            CustomerId = TestConstants.CustomerAlphaId,
            Items = new List<CreateSalesOrderItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 1, UnitPrice = 10m }
            }
        };

        // First creation succeeds
        var res1 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Second creation with same OrderNumber in same company fails with Conflict
        var res2 = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            request);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task AddItem_DetailInheritsParentScope_ForeignOrder_ReturnsNotFound()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.VIEW", "SALES_ORDER.EDIT" });

        var fakeOrderId = Guid.NewGuid();
        var itemRequest = new CreateSalesOrderItemRequest
        {
            ProductId = TestConstants.ProductAlphaId,
            Quantity = 1,
            UnitPrice = 25m
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{fakeOrderId}/items",
            itemRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SalesOrder_FullLifecycle_Draft_Submit_Confirm_Cancel()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[]
            {
                "SALES_ORDER.VIEW",
                "SALES_ORDER.CREATE",
                "SALES_ORDER.EDIT",
                "SALES_ORDER.SUBMIT",
                "SALES_ORDER.CONFIRM",
                "SALES_ORDER.CANCEL"
            });

        // 1. Create Draft order without items
        var orderNum = $"SO-FLOW-{Guid.NewGuid():N}"[..15];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            new CreateSalesOrderRequest
            {
                OrderNumber = orderNum,
                CustomerId = TestConstants.CustomerAlphaId
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var order = await createRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(order);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);

        // 2. Add Item to Draft order
        var addItemRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/items",
            new CreateSalesOrderItemRequest
            {
                ProductId = TestConstants.ProductAlphaId,
                Quantity = 4,
                UnitPrice = 25m,
                DiscountRate = 0m,
                TaxRate = 10m
            });
        Assert.Equal(HttpStatusCode.OK, addItemRes.StatusCode);

        // 3. Submit Order
        var submitRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
        var submittedOrder = await submitRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(submittedOrder);
        Assert.Equal(SalesOrderStatus.Submitted, submittedOrder.Status);

        // 4. Verify cannot add items after submission
        var addItemAfterSubmitRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/items",
            new CreateSalesOrderItemRequest
            {
                ProductId = TestConstants.ProductAlphaId,
                Quantity = 1,
                UnitPrice = 10m
            });
        Assert.Equal(HttpStatusCode.Conflict, addItemAfterSubmitRes.StatusCode);

        // 5. Confirm Order
        var confirmRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/confirm",
            new SalesOrderDecisionRequest { Remarks = "Approved by branch manager" });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmedOrder = await confirmRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(confirmedOrder);
        Assert.Equal(SalesOrderStatus.Confirmed, confirmedOrder.Status);

        // 6. Cancel Order
        var cancelRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/cancel",
            new SalesOrderDecisionRequest { Remarks = "Customer requested cancellation" });
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelledOrder = await cancelRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(cancelledOrder);
        Assert.Equal(SalesOrderStatus.Cancelled, cancelledOrder.Status);
    }

    [Fact]
    public async Task SalesOrder_Confirm_WithoutConfirmPermission_ReturnsForbidden()
    {
        // User has CREATE, VIEW, SUBMIT, but NOT CONFIRM
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.SUBMIT" });

        var orderNum = $"SO-NOCONF-{Guid.NewGuid():N}"[..15];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            new CreateSalesOrderRequest
            {
                OrderNumber = orderNum,
                CustomerId = TestConstants.CustomerAlphaId,
                Items = new List<CreateSalesOrderItemRequest>
                {
                    new() { ProductId = TestConstants.ProductAlphaId, Quantity = 1, UnitPrice = 50m }
                }
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var order = await createRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);

        var confirmRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order!.Id}/confirm",
            new SalesOrderDecisionRequest { Remarks = "Unauthorized confirmation attempt" });

        Assert.Equal(HttpStatusCode.Forbidden, confirmRes.StatusCode);
    }

    [Fact]
    public async Task SalesOrder_PreventionOfUnintendedInventoryWrites_NoInventoryRecordsCreated()
    {
        // Verify that executing complete Sales Order flow DOES NOT mutate inventory balances or transactions!
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.SUBMIT", "SALES_ORDER.CONFIRM" });

        // Record initial inventory transaction count
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        int initialTxCount = await db.InventoryTransactions.CountAsync(x => x.TenantId == TestConstants.TenantAlphaId);
        int initialBalanceCount = await db.InventoryBalances.CountAsync(x => x.TenantId == TestConstants.TenantAlphaId);

        // Perform Sales Order creation and confirmation
        var orderNum = $"SO-NOINV-{Guid.NewGuid():N}"[..15];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            new CreateSalesOrderRequest
            {
                OrderNumber = orderNum,
                CustomerId = TestConstants.CustomerAlphaId,
                Items = new List<CreateSalesOrderItemRequest>
                {
                    new() { ProductId = TestConstants.ProductAlphaId, Quantity = 10, UnitPrice = 100m }
                }
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var order = await createRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);

        var confirmRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order!.Id}/confirm",
            new SalesOrderDecisionRequest { Remarks = "Confirmed without inventory mutation" });
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);

        // Verify inventory count is UNCHANGED
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        int finalTxCount = await verifyDb.InventoryTransactions.CountAsync(x => x.TenantId == TestConstants.TenantAlphaId);
        int finalBalanceCount = await verifyDb.InventoryBalances.CountAsync(x => x.TenantId == TestConstants.TenantAlphaId);

        Assert.Equal(initialTxCount, finalTxCount);
        Assert.Equal(initialBalanceCount, finalBalanceCount);
    }
}
