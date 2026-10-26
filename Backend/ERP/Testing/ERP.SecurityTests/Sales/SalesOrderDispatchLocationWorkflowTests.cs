using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Sales;

[Collection("SecurityTestCollection")]
public class SalesOrderDispatchLocationWorkflowTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesOrderDispatchLocationWorkflowTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task Scenario1_DefaultLocation_HasSufficientStock_EnablesDispatch()
    {
        // 1. Arrange: Setup confirmed order with physical product
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);

        // Deposit 25 units into default location (more than enough for 10 units)
        var item = order.Items.First();
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, item.ProductId, 25m);

        // 2. Act: Query dispatch availability for order
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var avail = await res.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);

        // 3. Assert: Default location can fulfil, Dispatch is enabled, selected location is default
        Assert.NotNull(avail);
        Assert.True(avail.CanDefaultLocationFulfil);
        Assert.True(avail.CanFulfil);
        Assert.False(avail.HasShortages);
        Assert.Equal(loc.WarehouseLocationId, avail.SelectedLocationId);
        Assert.Equal(loc.WarehouseLocationId, avail.DefaultLocationId);
        Assert.Single(avail.Lines);
        Assert.Equal(10m, avail.Lines[0].RemainingQuantity);
        Assert.True(avail.Lines[0].AvailableQuantity >= 10m);
        Assert.Equal(0m, avail.Lines[0].ShortageQuantity);
        Assert.True(avail.Lines[0].CanFulfil);
    }

    [Fact]
    public async Task Scenario2_DefaultLocation_InsufficientStock_AlternativeCanFulfil_PromptsAlternative()
    {
        // 1. Arrange: Setup confirmed order with physical product (requires 10 units)
        var (client, order, whDefault, locDefault) = await SetupConfirmedOrderAsync(quantity: 10m);
        var item = order.Items.First();

        // Create an alternative location
        WarehouseLocation locAlt;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            locAlt = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whDefault.WarehouseId);
        }

        // Default location has only 3 units (insufficient, shortage of 7)
        await DepositInventoryAsync(whDefault.WarehouseId, locDefault.WarehouseLocationId, item.ProductId, 3m);

        // Alternative location has 20 units (can fulfil entire order)
        await DepositInventoryAsync(whDefault.WarehouseId, locAlt.WarehouseLocationId, item.ProductId, 20m);

        // 2. Act: Query availability (defaults to default location)
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var avail = await res.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);

        // 3. Assert: Default cannot fulfil, but alternative is offered in EligibleLocations
        Assert.NotNull(avail);
        Assert.False(avail.CanDefaultLocationFulfil);
        Assert.False(avail.CanFulfil);
        Assert.True(avail.HasShortages);
        Assert.NotEmpty(avail.EligibleLocations);
        Assert.Contains(avail.EligibleLocations, el => el.LocationId == locAlt.WarehouseLocationId);

        // Check line shortage details
        Assert.Equal(10m, avail.Lines[0].RemainingQuantity);
        Assert.Equal(3m, avail.Lines[0].AvailableQuantity);
        Assert.Equal(7m, avail.Lines[0].ShortageQuantity);
        Assert.False(avail.Lines[0].CanFulfil);
    }

    [Fact]
    public async Task Scenario3_SelectedAlternativeLocation_IsSufficient_EnablesDispatchAfterRevalidation()
    {
        // 1. Arrange: Order requires 15 units. Default has 2. Alt has 30.
        var (client, order, wh, locDefault) = await SetupConfirmedOrderAsync(quantity: 15m);
        var item = order.Items.First();

        WarehouseLocation locAlt;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            locAlt = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId);
        }

        await DepositInventoryAsync(wh.WarehouseId, locDefault.WarehouseLocationId, item.ProductId, 2m);
        await DepositInventoryAsync(wh.WarehouseId, locAlt.WarehouseLocationId, item.ProductId, 30m);

        // 2. Act: User selects alternative location -> query availability with target locationId
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability?locationId={locAlt.WarehouseLocationId}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var avail = await res.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);

        // 3. Assert: Dispatch is enabled for the selected alternative location
        Assert.NotNull(avail);
        Assert.False(avail.CanDefaultLocationFulfil);
        Assert.True(avail.CanFulfil);
        Assert.False(avail.HasShortages);
        Assert.Equal(locAlt.WarehouseLocationId, avail.SelectedLocationId);
        Assert.Equal(15m, avail.Lines[0].RemainingQuantity);
        Assert.True(avail.Lines[0].AvailableQuantity >= 15m);
        Assert.Equal(0m, avail.Lines[0].ShortageQuantity);
        Assert.True(avail.Lines[0].CanFulfil);
    }

    [Fact]
    public async Task Scenario4_NoSingleLocation_CanFulfil_DisablesDispatch_And_ShowsShortages()
    {
        // 1. Arrange: Order requires 50 units. Default has 10. Alt has 15. No single location has 50.
        var (client, order, wh, locDefault) = await SetupConfirmedOrderAsync(quantity: 50m);
        var item = order.Items.First();

        WarehouseLocation locAlt;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            locAlt = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId);
        }

        await DepositInventoryAsync(wh.WarehouseId, locDefault.WarehouseLocationId, item.ProductId, 10m);
        await DepositInventoryAsync(wh.WarehouseId, locAlt.WarehouseLocationId, item.ProductId, 15m);

        // 2. Act: Query availability
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var avail = await res.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);

        // 3. Assert: Dispatch is disabled, no eligible locations exist, shortage is clearly reported
        Assert.NotNull(avail);
        Assert.False(avail.CanDefaultLocationFulfil);
        Assert.False(avail.CanFulfil);
        Assert.True(avail.HasShortages);
        Assert.Empty(avail.EligibleLocations);
        Assert.Single(avail.Lines);
        Assert.Equal(50m, avail.Lines[0].RemainingQuantity);
        Assert.Equal(10m, avail.Lines[0].AvailableQuantity);
        Assert.Equal(40m, avail.Lines[0].ShortageQuantity);
        Assert.False(avail.Lines[0].CanFulfil);
    }

    [Fact]
    public async Task Scenario5_PreviouslyDispatchedQuantities_AreExcludedFromRemainingDemand()
    {
        // 1. Arrange: Order requires 20 units.
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 20m);
        var item = order.Items.First();
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, item.ProductId, 50m);

        // Partially dispatch 8 units using existing shipment dispatch
        var partialRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"PART-{Guid.NewGuid():N}"[..12],
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = item.Id,
                        ProductId = item.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 8m
                    }
                }
            });
        var partialShipment = await partialRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{partialShipment!.Id}/dispatch",
            null);

        // 2. Act: Check dispatch availability for remaining demand
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var avail = await res.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);

        // 3. Assert: Remaining demand is exactly 12 (20 - 8), NOT 20
        Assert.NotNull(avail);
        Assert.Single(avail.Lines);
        Assert.Equal(20m, avail.Lines[0].OrderedQuantity);
        Assert.Equal(8m, avail.Lines[0].AlreadyDispatchedQuantity);
        Assert.Equal(12m, avail.Lines[0].RemainingQuantity);
        Assert.True(avail.CanFulfil);
    }

    [Fact]
    public async Task Scenario6_StockChangesAfterCheck_RejectsInsufficientStockSafely()
    {
        // 1. Arrange: Order for 10 units. Location has 0 units initially.
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);
        var item = order.Items.First();

        // Stock was not deposited (or depleted)
        // 2. Act: Attempt direct dispatch via single-location dispatch endpoint
        var dispatchReq = new DispatchOrderRequest
        {
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            ShipmentNumber = $"DSP-FAIL-{Guid.NewGuid():N}"[..14]
        };

        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            dispatchReq);

        // 3. Assert: Backend rejects with error (status >= 400)
        Assert.False(postRes.IsSuccessStatusCode);

        // Verify no shipment was created and order remains Confirmed
        var orderRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var reloadedOrder = await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.Equal(SalesOrderStatus.Confirmed, reloadedOrder!.Status);
    }

    [Fact]
    public async Task Scenario7_SuccessfulDispatch_PostsCorrectStockIssue_ExactlyOnce()
    {
        // 1. Arrange: Order requires 10 units. Location has 30 units.
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);
        var item = order.Items.First();
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, item.ProductId, 30m);

        // 2. Act: Perform single-location dispatch
        var dispatchReq = new DispatchOrderRequest
        {
            WarehouseId = wh.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            ShipmentNumber = $"DSP-OK-{Guid.NewGuid():N}"[..14],
            Method = FulfillmentMethod.Delivery,
            RecipientName = "Receiving Officer"
        };

        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            dispatchReq);

        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var shipment = await postRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // 3. Assert: Shipment is Dispatched, exactly 10 units deducted, Order is Fulfilled
        Assert.NotNull(shipment);
        Assert.Equal(SalesShipmentStatus.Dispatched, shipment.Status);
        Assert.Single(shipment.Items);
        Assert.Equal(10m, shipment.Items[0].QuantityDispatched);

        // Balance should be exactly 20 (30 - 10)
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, item.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(20m, balance.QuantityOnHand);

        // Parent Sales Order transitioned to Fulfilled
        var orderRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var reloadedOrder = await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.Equal(SalesOrderStatus.Fulfilled, reloadedOrder!.Status);

        // Repeated submission of dispatch against already fulfilled order must fail
        var secondRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            dispatchReq);

        Assert.False(secondRes.IsSuccessStatusCode);

        // Stock must still be 20 (no double deduction)
        var balanceAfter = await GetInventoryBalanceAsync(loc.WarehouseLocationId, item.ProductId);
        Assert.Equal(20m, balanceAfter!.QuantityOnHand);
    }

    [Fact]
    public async Task Scenario8_TenantAndScopeAuthorization_RemainEnforced()
    {
        // 1. Arrange: Order created in CompanyA / BranchA1
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 5m);

        // 2. Act: Attempt to access availability using Company B scope (unauthorized for this order)
        var unauthorizedRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/sales-shipments/from-order/{order.Id}/availability");

        // 3. Assert: Must return 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedRes.StatusCode);

        // Attempt dispatch across company boundary
        var unauthDispatch = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId
            });

        Assert.Equal(HttpStatusCode.Forbidden, unauthDispatch.StatusCode);
    }

    // Helper methods
    private async Task<(HttpClient Client, SalesOrderDto Order, Warehouse Wh, WarehouseLocation Loc)> SetupConfirmedOrderAsync(decimal quantity)
    {
        var client = CreateBranchAdminClient();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id);
        var loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId);
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId);

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, quantity);
        return (client, order, wh, loc);
    }

    private HttpClient CreateBranchAdminClient()
    {
        return _fixture.CreateAuthenticatedClient(
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
                "SALES_SHIPMENT.VIEW",
                "SALES_SHIPMENT.CREATE",
                "SALES_SHIPMENT.EDIT",
                "SALES_SHIPMENT.DISPATCH",
                "SALES_SHIPMENT.DELIVER",
                "SALES_SHIPMENT.CANCEL"
            });
    }

    private async Task<SalesOrderDto> CreateConfirmedSalesOrderAsync(HttpClient client, Guid productId, decimal quantity)
    {
        var orderNum = $"SO-LOC-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            new CreateSalesOrderRequest
            {
                OrderNumber = orderNum,
                CustomerId = TestConstants.CustomerAlphaId,
                Items = new List<CreateSalesOrderItemRequest>
                {
                    new()
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        UnitPrice = 50m
                    }
                }
            });
        var order = await createRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);

        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order!.Id}/submit",
            null);

        var confRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/confirm",
            null);

        return (await confRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
    }

    private async Task DepositInventoryAsync(Guid warehouseId, Guid locationId, Guid productId, decimal quantity)
    {
        using var scope = _fixture.Services.CreateScope();
        var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();

        await postingService.PostMovementAsync(TestConstants.TenantAlphaId, new PostInventoryMovementRequest
        {
            MovementType = InventoryMovementType.StockAdjustmentIn,
            SourceDocumentType = SourceDocumentType.StockAdjustment,
            SourceDocumentId = Guid.NewGuid(),
            SourceDocumentLineId = Guid.NewGuid(),
            MovementSequence = 1,
            ProductId = productId,
            WarehouseId = warehouseId,
            WarehouseLocationId = locationId,
            Quantity = quantity,
            UnitCost = 10m,
            TransactionDate = DateTime.UtcNow
        });
    }

    private async Task<InventoryBalance?> GetInventoryBalanceAsync(Guid locationId, Guid productId)
    {
        using var scope = _fixture.Services.CreateScope();
        var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        return await postingService.GetBalanceAsync(TestConstants.TenantAlphaId, locationId, productId);
    }

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId, Guid? companyId = null, Guid? branchId = null)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..8];
        var wh = Warehouse.Create(
            tenantId,
            code,
            $"Warehouse_{code}",
            city: "Colombo",
            companyId: companyId,
            branchId: branchId);
        await db.Warehouses.AddAsync(wh);
        await db.SaveChangesAsync();
        return wh;
    }

    private static async Task<WarehouseLocation> CreateTestLocationAsync(DomainDbContext db, Guid tenantId, Guid warehouseId)
    {
        string code = $"LOC_{Guid.NewGuid():N}"[..8];
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

    private async Task<Product> CreateTestProductAsync(Guid tenantId)
    {
        string code = $"PRD_{Guid.NewGuid():N}"[..8];
        var prod = Product.Create(
            tenantId,
            code,
            $"Product {code}",
            TestConstants.CategoryAlphaId,
            unitOfMeasure: "PCS",
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
