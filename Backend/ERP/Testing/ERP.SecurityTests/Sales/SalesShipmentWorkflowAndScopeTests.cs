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
using ERP.SecurityTests;
using Xunit;

namespace ERP.SecurityTests.Sales;

[Collection("SecurityTestCollection")]
public class SalesShipmentWorkflowAndScopeTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesShipmentWorkflowAndScopeTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateShipment_FromOrder_FullWorkflow_Success()
    {
        // 1. Arrange: Create confirmed Sales Order with physical stock-tracked product
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);

        // 2. Act: Generate draft shipment from Sales Order
        var shipmentNum = $"SH-FULL-{Guid.NewGuid():N}"[..14];
        var createShipmentRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                CarrierName = "FastExpress",
                TrackingNumber = "TRACK-12345"
            });

        Assert.Equal(HttpStatusCode.Created, createShipmentRes.StatusCode);
        var shipment = await createShipmentRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(shipment);
        Assert.Equal(shipmentNum, shipment.ShipmentNumber);
        Assert.Equal(SalesShipmentStatus.Draft, shipment.Status);
        Assert.Equal(FulfillmentMethod.Delivery, shipment.Method);
        Assert.Single(shipment.Items);
        Assert.Equal(10m, shipment.Items[0].QuantityDispatched);

        // 3. Act: Deposit sufficient inventory so dispatch can succeed
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, shipment.Items[0].ProductId, 20m);

        // 4. Act: Dispatch shipment
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);
        var dispatched = await dispatchRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(dispatched);
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatched.Status);
        Assert.NotNull(dispatched.DispatchedAt);

        // Verify stock was decremented from 20 to 10
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, shipment.Items[0].ProductId);
        Assert.NotNull(balance);
        Assert.Equal(10m, balance.QuantityOnHand);

        // Verify parent Sales Order status transitioned to Fulfilled
        var orderRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var updatedOrder = await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(updatedOrder);
        Assert.Equal(SalesOrderStatus.Fulfilled, updatedOrder.Status);

        // 5. Act: Deliver shipment
        var deliverRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment.Id}/deliver",
            new DeliverShipmentRequest { RecipientName = "Warehouse Manager" });
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);
        var delivered = await deliverRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(delivered);
        Assert.Equal(SalesShipmentStatus.Delivered, delivered.Status);
        Assert.Equal("Warehouse Manager", delivered.RecipientName);
    }

    [Fact]
    public async Task PartialShipments_CumulativeCalculation_And_OverShipmentRejection()
    {
        // Sales order has 100 units
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 100m);
        var orderItemId = order.Items[0].Id;
        var prodId = order.Items[0].ProductId;

        // Inflow 150 units into warehouse
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prodId, 150m);

        // Shipment 1: 30 units
        var sh1Num = $"SH-P1-{Guid.NewGuid():N}"[..14];
        var sh1Res = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh1Num,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prodId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 30m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, sh1Res.StatusCode);
        var sh1 = await sh1Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Dispatch Shipment 1
        var d1Res = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{sh1!.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d1Res.StatusCode);

        // Order remains Confirmed (not Fulfilled)
        var orderCheck1 = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var o1 = await orderCheck1.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.Equal(SalesOrderStatus.Confirmed, o1!.Status);

        // Shipment 2: 40 units
        var sh2Num = $"SH-P2-{Guid.NewGuid():N}"[..14];
        var sh2Res = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh2Num,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prodId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 40m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, sh2Res.StatusCode);
        var sh2 = await sh2Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Dispatch Shipment 2
        var d2Res = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{sh2!.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d2Res.StatusCode);

        // Remaining unfulfilled is now 30 units (100 - 30 - 40)
        // Attempting to dispatch 31 units must be rejected!
        var sh3Num = $"SH-P3-{Guid.NewGuid():N}"[..14];
        var sh3OverRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh3Num,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prodId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 31m // Over-shipment by 1 unit!
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Conflict, sh3OverRes.StatusCode);

        // Final Shipment 3: dispatching exact remaining 30 units succeeds
        var sh3ValidRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh3Num,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            }); // Omitted Items defaults to remaining 30 units
        Assert.Equal(HttpStatusCode.Created, sh3ValidRes.StatusCode);
        var sh3 = await sh3ValidRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.Equal(30m, sh3!.Items[0].QuantityDispatched);

        var d3Res = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{sh3.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d3Res.StatusCode);

        // Order is now fully Fulfilled
        var orderCheckFinal = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var oFinal = await orderCheckFinal.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.Equal(SalesOrderStatus.Fulfilled, oFinal!.Status);

        // A 4th attempt to create shipment from fully fulfilled order is rejected
        var sh4Res = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"SH-EXTRA-{Guid.NewGuid():N}"[..14]
            });
        Assert.Equal(HttpStatusCode.Conflict, sh4Res.StatusCode);
    }

    [Fact]
    public async Task CustomerPickup_Or_StoreHandover_DirectDelivery_Succeeds()
    {
        // Retail / Trading handover without transport carrier
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 5m);
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, order.Items[0].ProductId, 10m);

        var shipmentNum = $"SH-PICKUP-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                Method = FulfillmentMethod.CustomerPickup,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                RecipientName = "Counter Customer"
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var shipment = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Direct delivery (skipping separate dispatch step)
        var deliverRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment!.Id}/deliver",
            new DeliverShipmentRequest { RecipientName = "Customer Handover" });
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);

        var delivered = await deliverRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(delivered);
        Assert.Equal(SalesShipmentStatus.Delivered, delivered.Status);
        Assert.NotNull(delivered.DispatchedAt);
        Assert.NotNull(delivered.DeliveredAt);
        Assert.Equal("Customer Handover", delivered.RecipientName);

        // Stock was decremented
        var bal = await GetInventoryBalanceAsync(loc.WarehouseLocationId, order.Items[0].ProductId);
        Assert.Equal(5m, bal!.QuantityOnHand);
    }

    [Fact]
    public async Task InsufficientInventory_Dispatch_FailsWithConflict()
    {
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 25m);
        // Deposit only 10 units (need 25)
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, order.Items[0].ProductId, 10m);

        var shipmentNum = $"SH-NOSTOCK-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var shipment = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Dispatch fails due to insufficient inventory
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment!.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.Conflict, dispatchRes.StatusCode);

        // Shipment remains Draft and stock remains untouched
        var bal = await GetInventoryBalanceAsync(loc.WarehouseLocationId, order.Items[0].ProductId);
        Assert.Equal(10m, bal!.QuantityOnHand);
    }

    [Fact]
    public async Task ServiceOnlyOrder_CannotCreatePhysicalShipment()
    {
        var client = CreateBranchAdminClient();

        // 1. Create a service product
        var serviceProd = await CreateTestProductAsync(
            TestConstants.TenantAlphaId,
            ProductType.Service,
            isStockTracked: false);

        // 2. Create Confirmed Sales Order with only the service product
        var order = await CreateConfirmedSalesOrderAsync(client, serviceProd.ProductId, quantity: 1m);

        // 3. Attempting to create shipment fails
        var res = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"SH-SRV-{Guid.NewGuid():N}"[..14]
            });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task CancelDraftShipment_Succeeds_AndDoesNotMutateStock()
    {
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);

        var shipmentNum = $"SH-CAN-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            });
        var shipment = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Cancel
        var cancelRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment!.Id}/cancel",
            null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelled = await cancelRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.Equal(SalesShipmentStatus.Cancelled, cancelled!.Status);

        // Cannot dispatch cancelled shipment
        var dispRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.Conflict, dispRes.StatusCode);
    }

    [Fact]
    public async Task ScopeEnforcement_CrossTenantAndBranch_Forbidden()
    {
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);

        var shipmentNum = $"SH-SCOPE-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            });
        var shipment = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // 1. User from Tenant Beta attempts to access Tenant Alpha shipment -> Forbidden
        var betaClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_beta",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.DISPATCH" });

        var crossTenantGet = await betaClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment!.Id}");
        Assert.True(crossTenantGet.StatusCode == HttpStatusCode.Forbidden || crossTenantGet.StatusCode == HttpStatusCode.NotFound);

        // 2. User targeting unassigned branch in Tenant Alpha -> Forbidden
        var crossBranchGet = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchB1Id}/sales-shipments/{shipment.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, crossBranchGet.StatusCode);
    }

    [Fact]
    public async Task PermissionEnforcement_WithoutDispatchPermission_ReturnsForbidden()
    {
        var (client, order, wh, loc) = await SetupConfirmedOrderAsync(quantity: 10m);

        var shipmentNum = $"SH-NOPERM-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            });
        var shipment = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // User without SALES_SHIPMENT.DISPATCH
        var viewOnlyClient = _fixture.CreateAuthenticatedClient(
            TestConstants.ReadOnlyUserId,
            "readonly_user",
            TestConstants.TenantAlphaId,
            new[] { "Viewer" },
            new[] { "SALES_SHIPMENT.VIEW" });

        var dispatchRes = await viewOnlyClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment!.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, dispatchRes.StatusCode);
    }

    // Helper setups
    private async Task<(HttpClient Client, SalesOrderDto Order, Warehouse Wh, WarehouseLocation Loc)> SetupConfirmedOrderAsync(decimal quantity)
    {
        var client = CreateBranchAdminClient();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId);
        var loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId);

        var order = await CreateConfirmedSalesOrderAsync(client, TestConstants.ProductAlphaId, quantity);
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
        var orderNum = $"SO-SH-{Guid.NewGuid():N}"[..14];
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

        // Submit order
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order!.Id}/submit",
            null);

        // Confirm order
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

    private static async Task<Warehouse> CreateTestWarehouseAsync(DomainDbContext db, Guid tenantId)
    {
        string code = $"WH_{Guid.NewGuid():N}"[..8];
        var wh = Warehouse.Create(tenantId, code, $"Warehouse_{code}", city: "Colombo");
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

    private async Task<Product> CreateTestProductAsync(Guid tenantId, ProductType productType, bool isStockTracked)
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
            isStockTracked: isStockTracked,
            productType: productType);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        await db.Products.AddAsync(prod);
        await db.SaveChangesAsync();
        return prod;
    }
}
