using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.MasterData.Customer;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class SalesOrderDeliveryAddressTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesOrderDeliveryAddressTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task SalesOrder_WithDeliveryAddress_CapturesSnapshotAndPreservesHistory()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "SALES_ORDER.CREATE",
                "SALES_ORDER.VIEW",
                "SALES_ORDER.SUBMIT",
                "SALES_ORDER.CONFIRM",
                "SALES_SHIPMENT.CREATE",
                "SALES_SHIPMENT.VIEW",
                "ADDRESS.CREATE",
                "ADDRESS.VIEW",
                "ADDRESS.EDIT",
                "ADDRESS.MANAGE",
                "CUSTOMER.VIEW"
            });

        // 1. Create an explicit Delivery Address in Tenant Alpha
        var addrRes = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            AddressName = "Warehouse 42 Dock",
            AddressLine1 = "42 Distribution Way",
            City = "Colombo",
            PostalCode = "00100"
        });
        Assert.Equal(HttpStatusCode.Created, addrRes.StatusCode);
        var addrJson = await addrRes.ReadDataJsonElementAsync();
        var addressId = Guid.Parse(addrJson.GetProperty("addressId").GetString()!);

        // 2. Create Sales Order pointing to this DeliveryAddressId
        var uniqueOrderNum = $"SO-{Guid.NewGuid():N}"[..15];
        var soRequest = new CreateSalesOrderRequest
        {
            OrderNumber = uniqueOrderNum,
            CustomerId = TestConstants.CustomerAlphaId,
            DeliveryAddressId = addressId,
            OrderDate = DateTime.UtcNow,
            Currency = "USD",
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

        var soRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders",
            soRequest);
        Assert.Equal(HttpStatusCode.Created, soRes.StatusCode);
        var createdOrder = await soRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(createdOrder);
        Assert.Equal(addressId, createdOrder.DeliveryAddressId);
        Assert.NotNull(createdOrder.DeliveryAddressSnapshot);
        Assert.Contains("42 Distribution Way", createdOrder.DeliveryAddressSnapshot);
        Assert.Contains("Colombo", createdOrder.DeliveryAddressSnapshot);

        var originalSnapshot = createdOrder.DeliveryAddressSnapshot;

        // 3. Mutate the master address in the database
        var updateAddrRes = await client.PutAsJsonAsync($"/api/addresses/{addressId}", new UpdateAddressRequest
        {
            AddressName = "Completely Altered Name",
            AddressLine1 = "999 New Street Road",
            City = "Kandy",
            PostalCode = "20000"
        });
        Assert.Equal(HttpStatusCode.OK, updateAddrRes.StatusCode);

        // 4. Verify historical stability: Reload Sales Order and ensure snapshot has NOT changed
        var reloadSoRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{createdOrder.Id}");
        Assert.Equal(HttpStatusCode.OK, reloadSoRes.StatusCode);
        var reloadedOrder = await reloadSoRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(reloadedOrder);
        Assert.Equal(originalSnapshot, reloadedOrder.DeliveryAddressSnapshot);
        Assert.DoesNotContain("999 New Street Road", reloadedOrder.DeliveryAddressSnapshot);

        // 5. Submit and Confirm Sales Order
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{createdOrder.Id}/submit", null);
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{createdOrder.Id}/confirm", null);

        // 6. Generate Dispatch Note (SalesShipment) from Sales Order
        var shipNum = $"DN-{Guid.NewGuid():N}"[..15];
        var shipRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{createdOrder.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipNum,
                Method = FulfillmentMethod.Delivery
            });
        Assert.Equal(HttpStatusCode.Created, shipRes.StatusCode);
        var createdShipment = await shipRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(createdShipment);
        // Verify shipment automatically inherits the order's delivery address snapshot
        Assert.Equal(originalSnapshot, createdShipment.DeliveryAddress);
    }
}
