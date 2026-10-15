using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.SecurityTests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Sales;

[Collection("SecurityTestCollection")]
public class SalesSimulationAndReplenishmentTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] FullPermissions = new[]
    {
        "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "LOCATION.CREATE",
        "PRODUCT.VIEW", "PRODUCT.CREATE", "PRODUCT.EDIT",
        "CUSTOMER.VIEW", "CUSTOMER.CREATE",
        "SUPPLIER.VIEW", "SUPPLIER.CREATE",
        "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.SUBMIT", "PURCHASE_ORDER.APPROVE",
        "GRN.VIEW", "GRN.CREATE", "GRN.EDIT", "GRN.CONFIRM", "GRN.CANCEL",
        "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.EDIT", "SALES_ORDER.SUBMIT", "SALES_ORDER.APPROVE", "SALES_ORDER.CONFIRM", "SALES_ORDER.CANCEL",
        "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.CREATE", "SALES_SHIPMENT.EDIT", "SALES_SHIPMENT.DISPATCH", "SALES_SHIPMENT.DELIVER", "SALES_SHIPMENT.CANCEL"
    };

    public SalesSimulationAndReplenishmentTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private HttpClient CreateAdminClient(Guid? tenantId = null, Guid? userId = null)
    {
        return _fixture.CreateAuthenticatedClient(
            userId: userId ?? TestConstants.TenantAdminAlphaId,
            username: "TenantAdminAlpha",
            tenantId: tenantId ?? TestConstants.TenantAlphaId,
            roles: new[] { "TenantAdmin" },
            permissions: FullPermissions);
    }

    // Context record holding master data IDs for simulation scenarios
    private record SimulationContext(
        HttpClient Client,
        Guid CompanyId,
        Guid BranchId,
        Guid WarehouseId,
        Guid LocationId,
        Guid SupplierId,
        Guid CustomerId,
        Guid Goods001Id,
        Guid Goods002Id,
        Guid Goods003Id,
        Guid Service001Id);

    private static Guid ExtractId(JsonElement doc, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (doc.TryGetProperty(prop, out var val))
            {
                return val.GetGuid();
            }
        }
        throw new InvalidOperationException($"None of the properties [{string.Join(", ", propertyNames)}] found in JSON response.");
    }

    private async Task<SimulationContext> SetupSimulationContextAsync()
    {
        var client = CreateAdminClient();
        Guid companyId = TestConstants.CompanyAId;
        Guid branchId = TestConstants.BranchA1Id;

        // 1. Create Warehouse via API
        string whCode = $"WH_{Guid.NewGuid():N}";
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            CompanyId = companyId,
            BranchId = branchId,
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var whDoc = await whRes.ReadDataJsonElementAsync();
        Guid warehouseId = ExtractId(whDoc, "warehouseId", "WarehouseId", "id", "Id");

        // 2. Create Location via API
        string locCode = $"LOC_{Guid.NewGuid():N}";
        var locRes = await client.PostAsJsonAsync($"/api/warehouses/{warehouseId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = locCode,
            LocationName = $"Location {locCode}"
        });
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
        var locDoc = await locRes.ReadDataJsonElementAsync();
        Guid locationId = ExtractId(locDoc, "warehouseLocationId", "WarehouseLocationId", "id", "Id");

        // 3. Create Supplier via API
        string supCode = $"SUP_{Guid.NewGuid():N}";
        var supRes = await client.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = supCode,
            SupplierName = $"Supplier {supCode}",
            City = "Colombo",
            Email = $"{supCode.ToLowerInvariant()}@supplier.com"
        });
        Assert.True(supRes.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {supRes.StatusCode}: {await supRes.Content.ReadAsStringAsync()}");
        var supDoc = await supRes.ReadDataJsonElementAsync();
        Guid supplierId = ExtractId(supDoc, "supplierId", "SupplierId", "id", "Id");

        // 4. Create Customer via API
        string custCode = $"CUST_{Guid.NewGuid():N}";
        var custRes = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = custCode,
            CustomerName = $"Customer {custCode}",
            City = "Colombo",
            CreditLimit = 100000m,
            Email = $"{custCode.ToLowerInvariant()}@client.com"
        });
        Assert.True(custRes.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {custRes.StatusCode}: {await custRes.Content.ReadAsStringAsync()}");
        var custDoc = await custRes.ReadDataJsonElementAsync();
        Guid customerId = ExtractId(custDoc, "customerId", "CustomerId", "id", "Id");

        // 5. Create 3 Physical Stock-Tracked Products and 1 Service Product via API
        // Product 1: SIM-GOODS-001 (Retail / Wholesale / Fast-moving SKU)
        string p1Code = $"G001_{Guid.NewGuid():N}";
        var p1Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p1Code,
            ProductName = $"SIM-GOODS-001 FMCG {p1Code}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 50.00m,
            StandardCost = 25.00m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.True(p1Res.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {p1Res.StatusCode}: {await p1Res.Content.ReadAsStringAsync()}");
        var p1Doc = await p1Res.ReadDataJsonElementAsync();
        Guid goods001Id = ExtractId(p1Doc, "productId", "ProductId", "id", "Id");

        // Product 2: SIM-GOODS-002 (Distribution / Bulk SKU)
        string p2Code = $"G002_{Guid.NewGuid():N}";
        var p2Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p2Code,
            ProductName = $"SIM-GOODS-002 Bulk Drums {p2Code}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 120.00m,
            StandardCost = 75.00m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.True(p2Res.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {p2Res.StatusCode}: {await p2Res.Content.ReadAsStringAsync()}");
        var p2Doc = await p2Res.ReadDataJsonElementAsync();
        Guid goods002Id = ExtractId(p2Doc, "productId", "ProductId", "id", "Id");

        // Product 3: SIM-GOODS-003 (Trading / Resale SKU)
        string p3Code = $"G003_{Guid.NewGuid():N}";
        var p3Res = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = p3Code,
            ProductName = $"SIM-GOODS-003 Hardware {p3Code}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 85.00m,
            StandardCost = 45.00m,
            IsPurchasable = true,
            IsSellable = true,
            IsStockTracked = true,
            ProductType = ProductType.StockItem
        });
        Assert.True(p3Res.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {p3Res.StatusCode}: {await p3Res.Content.ReadAsStringAsync()}");
        var p3Doc = await p3Res.ReadDataJsonElementAsync();
        Guid goods003Id = ExtractId(p3Doc, "productId", "ProductId", "id", "Id");

        // Product 4: SIM-SRV-001 (Service SKU)
        string srvCode = $"SRV_{Guid.NewGuid():N}";
        var srvRes = await client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = srvCode,
            ProductName = $"SIM-SRV-001 Service {srvCode}",
            CategoryId = TestConstants.CategoryAlphaId,
            UnitOfMeasure = "PCS",
            SellingPrice = 150.00m,
            StandardCost = 0m,
            IsPurchasable = false,
            IsSellable = true,
            IsStockTracked = false,
            ProductType = ProductType.Service
        });
        Assert.True(srvRes.StatusCode == HttpStatusCode.Created, $"Expected Created (201) but got {srvRes.StatusCode}: {await srvRes.Content.ReadAsStringAsync()}");
        var srvDoc = await srvRes.ReadDataJsonElementAsync();
        Guid service001Id = ExtractId(srvDoc, "productId", "ProductId", "id", "Id");

        return new SimulationContext(client, companyId, branchId, warehouseId, locationId, supplierId, customerId, goods001Id, goods002Id, goods003Id, service001Id);
    }

    private async Task ReplenishStockViaApiAsync(
        HttpClient client,
        Guid companyId,
        Guid branchId,
        Guid supplierId,
        Guid warehouseId,
        Guid locationId,
        Guid productId,
        decimal quantity,
        decimal unitPrice = 25m)
    {
        // Segregation of duties: PO is created and submitted by BranchUserA1, then approved by TenantAdminAlpha
        var submitterClient = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.BranchUserA1Id,
            username: "branchuser_a1",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "Branch User" },
            permissions: FullPermissions);

        // 1. Create Purchase Order via API by BranchUserA1
        string poNum = $"PO_{Guid.NewGuid():N}"[..14];
        var poCreateRes = await submitterClient.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/purchase-orders",
            new CreatePurchaseOrderRequest
            {
                OrderNumber = poNum,
                SupplierId = supplierId,
                OrderDate = DateTime.UtcNow,
                Items = new List<CreatePurchaseOrderItemRequest>
                {
                    new()
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        UnitPrice = unitPrice
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, poCreateRes.StatusCode);
        var poDoc = await poCreateRes.ReadDataJsonElementAsync();
        Guid poId = ExtractId(poDoc, "id", "Id");

        // 2. Submit Purchase Order via API by BranchUserA1
        var poSubmitRes = await submitterClient.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/purchase-orders/{poId}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, poSubmitRes.StatusCode);
        var poSubmittedDoc = await poSubmitRes.ReadDataJsonElementAsync();
        int poStatus = poSubmittedDoc.TryGetProperty("status", out var sProp) ? sProp.GetInt32() : poSubmittedDoc.GetProperty("Status").GetInt32();

        // 3. If approval is required (status == PurchaseOrderStatus.Submitted / 2), approve PO with TenantAdminAlpha (segregated approver)
        if (poStatus == (int)PurchaseOrderStatus.Submitted)
        {
            var poApproveRes = await client.PostAsJsonAsync(
                $"/api/companies/{companyId}/branches/{branchId}/purchase-orders/{poId}/approve",
                new { Remarks = "Approved by independent manager" });
            if (poApproveRes.StatusCode != HttpStatusCode.OK)
            {
                var err = await poApproveRes.Content.ReadAsStringAsync();
                throw new Exception($"PO approve failed with {poApproveRes.StatusCode}: {err}");
            }
        }

        // 4. Create Goods Receipt Note Draft from PO via API
        string grnNum = $"GRN_{Guid.NewGuid():N}"[..14];
        var grnCreateRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/from-po/{poId}?grnNumber={grnNum}&warehouseId={warehouseId}&warehouseLocationId={locationId}",
            null);
        if (grnCreateRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await grnCreateRes.Content.ReadAsStringAsync();
            throw new Exception($"GRN creation failed with {grnCreateRes.StatusCode}: {err}");
        }
        var grn = (await grnCreateRes.ReadApiResponseDataAsync<GoodsReceiptNoteDto>(_jsonOpts))!;

        // 5. Confirm GRN via API -> Posts InventoryMovementType.GoodsReceipt directly to warehouse location
        var grnConfirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/goods-receipt-notes/{grn.GoodsReceiptNoteId}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, grnConfirmRes.StatusCode);
    }

    private async Task<InventoryBalance?> GetInventoryBalanceAsync(Guid locationId, Guid productId)
    {
        using var scope = _fixture.Services.CreateScope();
        var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        return await postingService.GetBalanceAsync(TestConstants.TenantAlphaId, locationId, productId);
    }

    private async Task<SalesOrderDto> CreateAndConfirmSalesOrderAsync(
        HttpClient client,
        Guid companyId,
        Guid branchId,
        Guid customerId,
        List<(Guid ProductId, decimal Quantity, decimal UnitPrice)> items)
    {
        string soNum = $"SO_{Guid.NewGuid():N}"[..14];
        var createSoRes = await client.PostAsJsonAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-orders",
            new CreateSalesOrderRequest
            {
                OrderNumber = soNum,
                CustomerId = customerId,
                OrderDate = DateTime.UtcNow,
                Items = items.Select(i => new CreateSalesOrderItemRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            });
        Assert.Equal(HttpStatusCode.Created, createSoRes.StatusCode);
        var order = (await createSoRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;

        // Submit
        var submitRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-orders/{order.Id}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        // Confirm
        var confirmRes = await client.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-orders/{order.Id}/confirm",
            null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        return (await confirmRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
    }

    [Fact]
    public async Task Section4_ApiInventoryReplenishment_VerifiesStockBalances()
    {
        // Arrange Context
        var ctx = await SetupSimulationContextAsync();

        // Check Initial balances are 0 or null
        var initB1 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.True(initB1 == null || initB1.QuantityOnHand == 0m);

        // Replenish 200 units of SIM-GOODS-001
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 200m);
        var b1 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(b1);
        Assert.Equal(200m, b1.QuantityOnHand);
        Assert.Equal(200m, b1.QuantityAvailable);

        // Replenish 150 units of SIM-GOODS-002
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods002Id, 150m);
        var b2 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods002Id);
        Assert.NotNull(b2);
        Assert.Equal(150m, b2.QuantityOnHand);
        Assert.Equal(150m, b2.QuantityAvailable);

        // Replenish 80 units of SIM-GOODS-003
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods003Id, 80m);
        var b3 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id);
        Assert.NotNull(b3);
        Assert.Equal(80m, b3.QuantityOnHand);
        Assert.Equal(80m, b3.QuantityAvailable);
    }

    [Fact]
    public async Task ScenarioA_Retail_StoreHandover_CustomerPickup_Success()
    {
        var ctx = await SetupSimulationContextAsync();
        // Replenish 50 units
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 50m);

        // 1. Create confirmed Sales Order for 5 units
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods001Id, 5m, 50.00m) });

        // 2. Create shipment with StoreHandover
        string shNum = $"SH-RET-{Guid.NewGuid():N}"[..14];
        var shRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shNum,
                Method = FulfillmentMethod.StoreHandover,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                RecipientName = "Walk-in Retail Customer",
                Notes = "Counter pickup completed"
            });
        Assert.Equal(HttpStatusCode.Created, shRes.StatusCode);
        var shipment = (await shRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Equal(SalesShipmentStatus.Draft, shipment.Status);

        // 3. Deliver directly from draft (StoreHandover / CustomerPickup pattern)
        var deliverRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/deliver",
            new DeliverShipmentRequest { RecipientName = "Walk-in Retail Customer" });
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);
        var delivered = (await deliverRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Equal(SalesShipmentStatus.Delivered, delivered.Status);

        // 4. Verify stock decreased by 5: 50 -> 45
        var balance = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(balance);
        Assert.Equal(45m, balance.QuantityOnHand);

        // 5. Verify Sales Order transitioned to Fulfilled
        var orderRes = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-orders/{order.Id}");
        var finalOrder = (await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
        Assert.Equal(SalesOrderStatus.Fulfilled, finalOrder.Status);
    }

    [Fact]
    public async Task ScenarioB_Wholesale_PartialShipments_CumulativeCalculation()
    {
        var ctx = await SetupSimulationContextAsync();
        // Replenish 120 units
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 120m);

        // 1. Create confirmed Sales Order for 100 units
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods001Id, 100m, 45.00m) });
        var orderLineId = order.Items[0].Id;

        // 2. Shipment 1 for 40 units
        string sh1Num = $"SH-W1-{Guid.NewGuid():N}"[..14];
        var sh1Res = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh1Num,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderLineId,
                        ProductId = ctx.Goods001Id,
                        WarehouseId = ctx.WarehouseId,
                        WarehouseLocationId = ctx.LocationId,
                        QuantityDispatched = 40m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, sh1Res.StatusCode);
        var sh1 = (await sh1Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // Dispatch Shipment 1
        var d1Res = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{sh1.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d1Res.StatusCode);

        // Stock decreased by 40: 120 -> 80
        var bAfterSh1 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(bAfterSh1);
        Assert.Equal(80m, bAfterSh1.QuantityOnHand);

        // Order remains Confirmed (partially fulfilled)
        var orderCheck1 = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-orders/{order.Id}");
        var o1 = (await orderCheck1.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
        Assert.Equal(SalesOrderStatus.Confirmed, o1.Status);

        // 3. Shipment 2: Auto-defaults to remaining unfulfilled quantity (60 units)
        string sh2Num = $"SH-W2-{Guid.NewGuid():N}"[..14];
        var sh2Res = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh2Num,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        Assert.Equal(HttpStatusCode.Created, sh2Res.StatusCode);
        var sh2 = (await sh2Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Single(sh2.Items);
        Assert.Equal(60m, sh2.Items[0].QuantityDispatched);

        // Dispatch Shipment 2
        var d2Res = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{sh2.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d2Res.StatusCode);

        // Stock decreased by additional 60: 80 -> 20 (cumulative 100)
        var bAfterSh2 = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(bAfterSh2);
        Assert.Equal(20m, bAfterSh2.QuantityOnHand);

        // Order status transitions to Fulfilled
        var orderCheck2 = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-orders/{order.Id}");
        var o2 = (await orderCheck2.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
        Assert.Equal(SalesOrderStatus.Fulfilled, o2.Status);
    }

    [Fact]
    public async Task ScenarioC_Distribution_CarrierAndTracking_DispatchThenDeliver()
    {
        var ctx = await SetupSimulationContextAsync();
        // Replenish 100 units of SIM-GOODS-002
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods002Id, 100m);

        // 1. Create confirmed Sales Order for 50 units
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods002Id, 50m, 120.00m) });

        // 2. Create shipment with full carrier and logistics details
        string shNum = $"SH-DIST-{Guid.NewGuid():N}"[..14];
        var shRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shNum,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                CarrierName = "BlueDart Logistics",
                TrackingNumber = "BD-LOG-987654321",
                RecipientName = "Regional Distribution Depot",
                DeliveryAddress = "Highway Logistics Hub, Unit 4",
                VehicleNumber = "WP-CAD-5544",
                Notes = "Fragile drums - keep upright"
            });
        Assert.Equal(HttpStatusCode.Created, shRes.StatusCode);
        var shipment = (await shRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // 3. Dispatch shipment: Stock decrements at dispatch (100 -> 50)
        var dispatchRes = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);
        var dispatched = (await dispatchRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatched.Status);
        Assert.NotNull(dispatched.DispatchedAt);

        var balanceAfterDispatch = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods002Id);
        Assert.NotNull(balanceAfterDispatch);
        Assert.Equal(50m, balanceAfterDispatch.QuantityOnHand);

        // 4. Confirm delivery in separate step
        var deliverRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/deliver",
            new DeliverShipmentRequest
            {
                RecipientName = "Regional Distribution Depot",
                Notes = "Signed proof of delivery POD-98765 received"
            });
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);
        var delivered = (await deliverRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Equal(SalesShipmentStatus.Delivered, delivered.Status);
        Assert.NotNull(delivered.DeliveredAt);

        // 5. Verify stock was NOT double-deducted (remains 50)
        var balanceAfterDelivery = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods002Id);
        Assert.NotNull(balanceAfterDelivery);
        Assert.Equal(50m, balanceAfterDelivery.QuantityOnHand);
    }

    [Fact]
    public async Task ScenarioD_Trading_PurchasedResaleStock_NoManufacturing()
    {
        var ctx = await SetupSimulationContextAsync();
        // 1. Buy trading stock via Procurement PO -> GRN API (Cost: $45)
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods003Id, 80m, unitPrice: 45.00m);

        var initBal = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id);
        Assert.NotNull(initBal);
        Assert.Equal(80m, initBal.QuantityOnHand);

        // 2. Sell trading stock via Sales Order (Price: $85)
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods003Id, 30m, 85.00m) });

        // 3. Fulfill via Shipment -> Dispatch -> Deliver without any manufacturing
        string shNum = $"SH-TRD-{Guid.NewGuid():N}"[..14];
        var shRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shNum,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        var shipment = (await shRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        var dRes = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dRes.StatusCode);

        var delivRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/deliver",
            new DeliverShipmentRequest { RecipientName = "Trading Reseller" });
        Assert.Equal(HttpStatusCode.OK, delivRes.StatusCode);

        // Stock decremented by 30: 80 -> 50
        var finalBal = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id);
        Assert.NotNull(finalBal);
        Assert.Equal(50m, finalBal.QuantityOnHand);

        // Order marked Fulfilled
        var orderRes = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-orders/{order.Id}");
        var finalOrder = (await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
        Assert.Equal(SalesOrderStatus.Fulfilled, finalOrder.Status);
    }

    [Fact]
    public async Task ScenarioE_StockReplenishment_FollowedBy_PartialSales()
    {
        var ctx = await SetupSimulationContextAsync();
        // Initial Stock: 50
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods003Id, 50m);
        var priorStock = (await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id))!.QuantityOnHand;
        Assert.Equal(50m, priorStock);

        // Replenish Additional Stock: 50
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods003Id, 50m);
        var postReplenishStock = (await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id))!.QuantityOnHand;
        Assert.Equal(100m, postReplenishStock);

        // Sell Partial Stock: 40 units
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods003Id, 40m, 85.00m) });

        string shNum = $"SH-REP-SALE-{Guid.NewGuid():N}"[..14];
        var shRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        var shipment = (await shRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        var dRes = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dRes.StatusCode);

        // Verify remaining stock formula: Prior Stock (50) + Replenished (50) - Dispatched (40) = 60
        var finalBal = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods003Id);
        Assert.NotNull(finalBal);
        Assert.Equal(60m, finalBal.QuantityOnHand);
        Assert.Equal(priorStock + 50m - 40m, finalBal.QuantityOnHand);
    }

    [Fact]
    public async Task EdgeCases_OverShipment_DuplicateDispatch_InsufficientStock_Unauthorized()
    {
        var ctx = await SetupSimulationContextAsync();
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 10m);

        // 1. Over-Shipment Rejection (Ordered 10, Attempt to ship 15)
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods001Id, 10m, 50.00m) });

        string overShipNum = $"SH-OVER-{Guid.NewGuid():N}"[..14];
        var overShipRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = overShipNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = order.Items[0].Id,
                        ProductId = ctx.Goods001Id,
                        WarehouseId = ctx.WarehouseId,
                        WarehouseLocationId = ctx.LocationId,
                        QuantityDispatched = 15m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Conflict, overShipRes.StatusCode);

        // 2. Duplicate Dispatch Idempotency
        // Valid shipment for 2 units
        string validShipNum = $"SH-IDEM-{Guid.NewGuid():N}"[..14];
        var validShRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = validShipNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = order.Items[0].Id,
                        ProductId = ctx.Goods001Id,
                        WarehouseId = ctx.WarehouseId,
                        WarehouseLocationId = ctx.LocationId,
                        QuantityDispatched = 2m
                    }
                }
            });
        var validSh = (await validShRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // First dispatch -> OK, stock 10 -> 8
        var d1Res = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{validSh.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d1Res.StatusCode);
        var bal1 = (await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id))!.QuantityOnHand;
        Assert.Equal(8m, bal1);

        // Second dispatch -> returns 200 OK idempotently, stock stays 8 (no double deduction)
        var d2Res = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{validSh.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, d2Res.StatusCode);
        var bal2 = (await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id))!.QuantityOnHand;
        Assert.Equal(8m, bal2);

        // 3. Insufficient Stock Handling (Attempt to ship 500 when available is 8)
        var largeOrder = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods001Id, 500m, 50.00m) });

        string largeShNum = $"SH-NOSTOCK-{Guid.NewGuid():N}"[..14];
        var largeShRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{largeOrder.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = largeShNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        var largeSh = (await largeShRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        var failDispatchRes = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{largeSh.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.Conflict, failDispatchRes.StatusCode);

        // Verify state is preserved: shipment is still Draft, stock is still 8
        var checkShRes = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{largeSh.Id}");
        var checkSh = (await checkShRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;
        Assert.Equal(SalesShipmentStatus.Draft, checkSh.Status);
        var balAfterFail = (await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id))!.QuantityOnHand;
        Assert.Equal(8m, balAfterFail);

        // 4. Cross-Tenant Unauthorized Access
        var clientBeta = CreateAdminClient(tenantId: TestConstants.TenantBetaId, userId: TestConstants.TenantAdminBetaId);
        var crossTenantRes = await clientBeta.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{validSh.Id}");
        Assert.True(crossTenantRes.StatusCode == HttpStatusCode.Forbidden || crossTenantRes.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ServiceVsGoods_ServiceOnly_And_MixedOrders()
    {
        var ctx = await SetupSimulationContextAsync();
        // Replenish 20 units of physical goods
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 20m);

        // 1. Service-Only Order: Fulfilling physical shipment is rejected
        var serviceOnlyOrder = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Service001Id, 5m, 150.00m) });

        string srvShNum = $"SH-SRV-{Guid.NewGuid():N}"[..14];
        var srvShRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{serviceOnlyOrder.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = srvShNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        Assert.Equal(HttpStatusCode.Conflict, srvShRes.StatusCode);

        // 2. Mixed Order: Physical Goods (10 units) + Service (2 hours)
        var mixedOrder = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new()
            {
                (ctx.Goods001Id, 10m, 50.00m),
                (ctx.Service001Id, 2m, 150.00m)
            });

        string mixedShNum = $"SH-MIX-{Guid.NewGuid():N}"[..14];
        var mixedShRes = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{mixedOrder.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = mixedShNum,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId
            });
        Assert.Equal(HttpStatusCode.Created, mixedShRes.StatusCode);
        var mixedSh = (await mixedShRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // Shipment must contain ONLY the 1 physical line item
        Assert.Single(mixedSh.Items);
        Assert.Equal(ctx.Goods001Id, mixedSh.Items[0].ProductId);
        Assert.Equal(10m, mixedSh.Items[0].QuantityDispatched);

        // Dispatch mixed shipment
        var dMixedRes = await ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{mixedSh.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dMixedRes.StatusCode);

        // Stock decreased by 10 for physical goods: 20 -> 10
        var bPhysical = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(bPhysical);
        Assert.Equal(10m, bPhysical.QuantityOnHand);

        // Service item has no stock movement
        var bService = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Service001Id);
        Assert.True(bService == null || bService.QuantityOnHand == 0);

        // Overall Sales Order remains Confirmed because service deliverables remain pending!
        var mixedOrderCheck = await ctx.Client.GetAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-orders/{mixedOrder.Id}");
        var finalMixedOrder = (await mixedOrderCheck.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
        Assert.Equal(SalesOrderStatus.Confirmed, finalMixedOrder.Status);
    }

    [Fact]
    public async Task ConcurrentOverShipment_SimultaneousDispatches_AreSerializedAndPrevented()
    {
        var ctx = await SetupSimulationContextAsync();
        // Replenish 20 units of physical goods
        await ReplenishStockViaApiAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.SupplierId,
            ctx.WarehouseId, ctx.LocationId, ctx.Goods001Id, 20m);

        // Sales Order for exactly 10 units
        var order = await CreateAndConfirmSalesOrderAsync(ctx.Client, ctx.CompanyId, ctx.BranchId, ctx.CustomerId,
            new() { (ctx.Goods001Id, 10m, 50.00m) });

        // Create Draft Shipment 1 for 6 units
        string sh1Num = $"SH-CONC-1-{Guid.NewGuid():N}"[..14];
        var sh1Res = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh1Num,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = order.Items[0].Id,
                        ProductId = ctx.Goods001Id,
                        WarehouseId = ctx.WarehouseId,
                        WarehouseLocationId = ctx.LocationId,
                        QuantityDispatched = 6m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, sh1Res.StatusCode);
        var sh1 = (await sh1Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // Create Draft Shipment 2 for 6 units (6 + 6 = 12 > 10 ordered!)
        string sh2Num = $"SH-CONC-2-{Guid.NewGuid():N}"[..14];
        var sh2Res = await ctx.Client.PostAsJsonAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = sh2Num,
                DefaultWarehouseId = ctx.WarehouseId,
                DefaultLocationId = ctx.LocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = order.Items[0].Id,
                        ProductId = ctx.Goods001Id,
                        WarehouseId = ctx.WarehouseId,
                        WarehouseLocationId = ctx.LocationId,
                        QuantityDispatched = 6m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, sh2Res.StatusCode);
        var sh2 = (await sh2Res.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts))!;

        // Execute both dispatches simultaneously via Task.WhenAll
        var task1 = ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{sh1.Id}/dispatch",
            null);
        var task2 = ctx.Client.PostAsync(
            $"/api/companies/{ctx.CompanyId}/branches/{ctx.BranchId}/sales-shipments/{sh2.Id}/dispatch",
            null);

        var responses = await Task.WhenAll(task1, task2);

        // Exactly one dispatch must succeed (200 OK) and the other must be rejected (409 Conflict)
        int okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        int conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, okCount);
        Assert.Equal(1, conflictCount);

        // Total stock deducted must be exactly 6 (20 -> 14), NOT 12!
        var bal = await GetInventoryBalanceAsync(ctx.LocationId, ctx.Goods001Id);
        Assert.NotNull(bal);
        Assert.Equal(14m, bal.QuantityOnHand);
    }
}
