using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ERP.Domain.Features.MasterData.Category;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.Inventory.Batch;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.Sales;

[Collection("SecurityTestCollection")]
public class SalesOrderUomConversionDispatchTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesOrderUomConversionDispatchTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task StagedDispatch_WithPackagingUom_ConvertsToBaseUomForInventoryPosting()
    {
        // 1. Arrange: Setup product with Base UOM = PCS, Packaging UOM = BOX (1 BOX = 12 PCS)
        var client = CreateBranchUserClient();
        var (prod, uomBox, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 12m);

        // Deposit 120 PCS in warehouse location
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 120m);

        // Create and confirm Sales Order for 5 BOX (= 60 PCS in base UOM)
        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);

        // 2. Act: Generate draft shipment from Sales Order (Staged workflow)
        var shipmentNum = $"SH-STG-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var createShipmentRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                CarrierName = "Apex Logistics",
                TrackingNumber = "APX-001"
            });

        Assert.Equal(HttpStatusCode.Created, createShipmentRes.StatusCode);
        var shipment = await createShipmentRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(shipment);
        Assert.Single(shipment.Items);

        // Invariant: The shipment line MUST retain the transaction UOM and transaction quantity
        Assert.Equal(5m, shipment.Items[0].QuantityDispatched);
        Assert.Equal(uomBox.UnitOfMeasureId, shipment.Items[0].UnitOfMeasureId);

        // 3. Act: Dispatch the staged shipment
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);
        var dispatched = await dispatchRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(dispatched);
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatched.Status);

        // 4. Assert: Inventory ledger posting MUST be in product Base UOM (PCS) with 60 PCS deducted
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(60m, balance.QuantityOnHand); // 120 - 60 = 60

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var ledgerTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == shipment.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);
        Assert.NotNull(ledgerTxn);
        Assert.Equal(-60m, ledgerTxn.Quantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, ledgerTxn.UnitOfMeasureId);

        // Parent order must be Fulfilled
        var updatedOrder = await GetSalesOrderAsync(client, order.Id);
        Assert.Equal(SalesOrderStatus.Fulfilled, updatedOrder.Status);
    }

    [Fact]
    public async Task StagedDispatch_PartialFulfillment_ConvertsCorrectProportionToBaseUom()
    {
        // 1. Arrange: 1 BOX = 12 PCS
        var client = CreateBranchUserClient();
        var (prod, uomBox, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 12m);

        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 100m);

        // Order 5 BOX
        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);

        // 2. Act: Create partial shipment for only 2 BOX (= 24 PCS in base UOM)
        var shipmentNum = $"SH-PART-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var orderLineId = order.Items[0].Id;
        var createShipmentRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                Method = FulfillmentMethod.Delivery,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderLineId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = loc.WarehouseLocationId,
                        QuantityDispatched = 2m
                    }
                }
            });

        Assert.Equal(HttpStatusCode.Created, createShipmentRes.StatusCode);
        var shipment = await createShipmentRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(shipment);
        Assert.Equal(2m, shipment.Items[0].QuantityDispatched);

        // 3. Act: Dispatch the partial shipment
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipment.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);

        // 4. Assert: Exactly 24 PCS deducted (2 BOX * 12 PCS/BOX)
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(76m, balance.QuantityOnHand); // 100 - 24 = 76

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var ledgerTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == shipment.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);
        Assert.NotNull(ledgerTxn);
        Assert.Equal(-24m, ledgerTxn.Quantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, ledgerTxn.UnitOfMeasureId);

        // Parent order remains Confirmed because 3 BOX remain unfulfilled
        var updatedOrder = await GetSalesOrderAsync(client, order.Id);
        Assert.Equal(SalesOrderStatus.Confirmed, updatedOrder.Status);
    }

    [Fact]
    public async Task DirectDispatch_WithPackagingUom_ConvertsToBaseUomForInventoryPosting()
    {
        // 1. Arrange: 1 BOX = 12 PCS
        var client = CreateBranchUserClient();
        var (prod, uomBox, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 12m);

        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 120m);

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);

        // 2. Act: Direct dispatch via CreateAndDispatchFromOrder
        var directRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId,
                ShipmentNumber = $"DSP-DIR-{Guid.NewGuid():N}"[..14].ToUpperInvariant()
            });

        Assert.Equal(HttpStatusCode.Created, directRes.StatusCode);
        var shipment = await directRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(shipment);
        Assert.Equal(SalesShipmentStatus.Dispatched, shipment.Status);

        // 3. Assert: Exactly 60 PCS deducted in base UOM
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(60m, balance.QuantityOnHand); // 120 - 60 = 60

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var ledgerTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == shipment.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);
        Assert.NotNull(ledgerTxn);
        Assert.Equal(-60m, ledgerTxn.Quantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, ledgerTxn.UnitOfMeasureId);

        var updatedOrder = await GetSalesOrderAsync(client, order.Id);
        Assert.Equal(SalesOrderStatus.Fulfilled, updatedOrder.Status);
    }

    [Fact]
    public async Task DirectAndStagedDispatch_Parity_ProducesIdenticalBaseUomLedgerMovements()
    {
        // 1. Arrange: 1 BOX = 10 PCS
        var client = CreateBranchUserClient();
        var (prod, uomBox, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 10m);

        // Deposit enough for two separate orders of 5 BOX each (50 + 50 = 100 PCS)
        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 200m);

        var orderStaged = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);
        var orderDirect = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);

        // 2. Act: Staged dispatch
        var stagedCreateRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{orderStaged.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"SH-PAR1-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
                Method = FulfillmentMethod.CustomerPickup,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = loc.WarehouseLocationId
            });
        var stagedShipment = await stagedCreateRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{stagedShipment!.Id}/dispatch",
            null);

        // 3. Act: Direct dispatch
        var directRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{orderDirect.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId,
                ShipmentNumber = $"DSP-PAR2-{Guid.NewGuid():N}"[..14].ToUpperInvariant()
            });
        var directShipment = await directRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // 4. Assert Parity: Both shipments posted exactly -50 PCS in base UOM
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var stagedTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == stagedShipment.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);
        var directTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == directShipment!.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);

        Assert.NotNull(stagedTxn);
        Assert.NotNull(directTxn);
        Assert.Equal(stagedTxn.Quantity, directTxn.Quantity);
        Assert.Equal(-50m, stagedTxn.Quantity);
        Assert.Equal(stagedTxn.UnitOfMeasureId, directTxn.UnitOfMeasureId);
        Assert.Equal(uomPcs.UnitOfMeasureId, stagedTxn.UnitOfMeasureId);

        // Final balance: 200 - 50 - 50 = 100 PCS
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(100m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task Dispatch_WithoutConversion_WhenItemInBaseUom_PreservesQuantityExactly()
    {
        // 1. Arrange: Order directly in product's base UOM (PCS)
        var client = CreateBranchUserClient();
        var (prod, _, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 12m);

        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 20m);

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomPcs.UnitOfMeasureId);

        // 2. Act: Direct dispatch
        var directRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId
            });

        Assert.Equal(HttpStatusCode.Created, directRes.StatusCode);
        var shipment = await directRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // 3. Assert: Exactly 5 PCS deducted without conversion factor
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(15m, balance.QuantityOnHand);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var ledgerTxn = await db.InventoryTransactions
            .FirstOrDefaultAsync(t => t.SourceDocumentId == shipment!.Id && t.MovementType == InventoryMovementType.ShipmentDispatch);
        Assert.NotNull(ledgerTxn);
        Assert.Equal(-5m, ledgerTxn.Quantity);
        Assert.Equal(uomPcs.UnitOfMeasureId, ledgerTxn.UnitOfMeasureId);
    }

    [Fact]
    public async Task AvailabilityCheck_And_DirectDispatch_InsufficientStock_Rejected()
    {
        // 1. Arrange: 1 BOX = 12 PCS. Demand is 5 BOX (= 60 PCS).
        // Only deposit 50 PCS (insufficient stock by 10 PCS).
        var client = CreateBranchUserClient();
        var (prod, uomBox, _, wh, loc) = await SetupProductWithPackagingAsync(conversionFactor: 12m);

        await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 50m);

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);

        // 2. Act & Assert: Check Availability API
        var availRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability?locationId={loc.WarehouseLocationId}");
        Assert.Equal(HttpStatusCode.OK, availRes.StatusCode);
        var availDto = await availRes.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);
        Assert.NotNull(availDto);
        Assert.False(availDto.CanFulfil);
        Assert.Single(availDto.Lines);
        Assert.False(availDto.Lines[0].CanFulfil);
        Assert.True(availDto.Lines[0].ShortageQuantity > 0m);

        // 3. Act & Assert: Direct dispatch must fail with 400 Bad Request / InvalidOperationException
        var directRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId
            });

        Assert.Equal(HttpStatusCode.Conflict, directRes.StatusCode);

        // Stock remains untouched at 50 PCS
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balance);
        Assert.Equal(50m, balance.QuantityOnHand);
    }

    [Fact]
    public async Task DirectDispatch_BatchFefoAllocation_AllocatesInBaseUnits()
    {
        // 1. Arrange: Batch-tracked product with 1 BOX = 12 PCS.
        var client = CreateBranchUserClient();
        var tenantId = TestConstants.TenantAlphaId;
        var (prod, uomBox, uomPcs, wh, loc) = await SetupProductWithPackagingAsync(
            conversionFactor: 12m,
            trackingMode: TrackingMode.Batch);

        // Deposit Batch 1: 60 PCS expiring early (2027-01-01)
        string batch1 = $"BAT1_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await DepositBatchInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 60m, batch1, new DateTime(2027, 1, 1));

        // Deposit Batch 2: 60 PCS expiring later (2027-06-01)
        string batch2 = $"BAT2_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await DepositBatchInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 60m, batch2, new DateTime(2027, 6, 1));

        // Order 5 BOX (= 60 PCS)
        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m, uomBox.UnitOfMeasureId);

        // 2. Act: Direct dispatch - FEFO should allocate from Batch 1
        var directRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            new DispatchOrderRequest
            {
                WarehouseId = wh.WarehouseId,
                WarehouseLocationId = loc.WarehouseLocationId
            });

        Assert.Equal(HttpStatusCode.Created, directRes.StatusCode);
        var shipment = await directRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(shipment);
        Assert.Single(shipment.Items);
        Assert.Equal(batch1, shipment.Items[0].BatchNumber);

        // 3. Assert: Batch 1 balance is exhausted (0 PCS), Batch 2 remains intact (60 PCS)
        var b1Balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId, batch1);
        Assert.NotNull(b1Balance);
        Assert.Equal(0m, b1Balance.QuantityOnHand);

        var b2Balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId, batch2);
        Assert.NotNull(b2Balance);
        Assert.Equal(60m, b2Balance.QuantityOnHand);
    }

    #region Helper Methods

    private HttpClient CreateBranchUserClient()
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

    private async Task<(Product Prod, UnitOfMeasure Box, UnitOfMeasure Pcs, Warehouse Wh, WarehouseLocation Loc)>
        SetupProductWithPackagingAsync(decimal conversionFactor, TrackingMode trackingMode = TrackingMode.None)
    {
        var tenantId = TestConstants.TenantAlphaId;
        await EnableSpecialUomAsync(tenantId, true);
        await SetCompanySpecialUomAsync(tenantId, TestConstants.CompanyAId, true);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var packagingService = scope.ServiceProvider.GetRequiredService<IProductPackagingService>();

        var cat = await db.Categories.FirstAsync(c => c.TenantId == tenantId && c.IsActive);
        var uomBox = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "BOX");
        var uomPcs = await db.UnitsOfMeasure.FirstAsync(u => u.TenantId == tenantId && u.Code == "PCS");

        string uid = Guid.NewGuid().ToString("N")[..8];
        var prod = Product.Create(
            tenantId,
            $"UOM_SO_{uid}",
            $"UOM Conversion Test Product {uid}",
            cat.CategoryId,
            unitOfMeasure: "PCS",
            unitOfMeasureId: uomPcs.UnitOfMeasureId,
            isPurchasable: true,
            isSellable: true,
            isStockTracked: true,
            productType: ProductType.StockItem,
            trackingMode: trackingMode);

        await db.Products.AddAsync(prod);

        var wh = Warehouse.Create(tenantId, $"WH_{uid}", $"WH {uid}", companyId: TestConstants.CompanyAId, branchId: TestConstants.BranchA1Id);
        await db.Warehouses.AddAsync(wh);

        var loc = WarehouseLocation.Create(tenantId, wh.WarehouseId, null, $"LOC_{uid}", $"Location {uid}");
        await db.WarehouseLocations.AddAsync(loc);

        await db.SaveChangesAsync();

        // Create Packaging: 1 BOX = conversionFactor PCS
        await packagingService.CreatePackagingAsync(tenantId, new CreateProductPackagingRequest
        {
            ProductId = prod.ProductId,
            PackagingCode = $"PKG_{uid}",
            PackagingName = $"Box of {conversionFactor} Pieces",
            PackagingUomId = uomBox.UnitOfMeasureId,
            BaseQuantity = conversionFactor,
            PurchaseAllowed = true,
            SalesAllowed = true,
            CompanyId = TestConstants.CompanyAId
        });

        return (prod, uomBox, uomPcs, wh, loc);
    }

    private async Task<SalesOrderDto> CreateConfirmedSalesOrderAsync(HttpClient client, Guid productId, decimal quantity, Guid? uomId = null)
    {
        var orderNum = $"SO-UOM-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
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
                        UnitPrice = 50m,
                        UnitOfMeasureId = uomId
                    }
                }
            });

        if (createRes.StatusCode != HttpStatusCode.Created)
        {
            var err = await createRes.Content.ReadAsStringAsync();
            throw new Exception($"CreateSalesOrder failed with {createRes.StatusCode}: {err}");
        }
        var order = await createRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);

        // Submit
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order!.Id}/submit",
            null);

        // Confirm
        var confRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}/confirm",
            null);

        return (await confRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
    }

    private async Task<SalesOrderDto> GetSalesOrderAsync(HttpClient client, Guid orderId)
    {
        var res = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{orderId}");
        return (await res.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts))!;
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

    private async Task DepositBatchInventoryAsync(Guid warehouseId, Guid locationId, Guid productId, decimal quantity, string batchNumber, DateTime expiryDate)
    {
        using var scope = _fixture.Services.CreateScope();
        var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        // Ensure ProductBatch record exists
        var batch = await db.ProductBatches.FirstOrDefaultAsync(b => b.TenantId == TestConstants.TenantAlphaId && b.ProductId == productId && b.BatchNumber == batchNumber);
        if (batch == null)
        {
            batch = ProductBatch.Create(
                TestConstants.TenantAlphaId,
                productId,
                batchNumber,
                expiryDate: expiryDate);
            await db.ProductBatches.AddAsync(batch);
            await db.SaveChangesAsync();
        }

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
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            TransactionDate = DateTime.UtcNow
        });
    }

    private async Task<InventoryBalance?> GetInventoryBalanceAsync(Guid locationId, Guid productId, string? batchNumber = null)
    {
        using var scope = _fixture.Services.CreateScope();
        var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
        return await postingService.GetBalanceAsync(TestConstants.TenantAlphaId, locationId, productId, batchNumber: batchNumber);
    }

    private async Task EnableSpecialUomAsync(Guid tenantId, bool enabled)
    {
        using var scope = _fixture.Services.CreateScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<SaaS.Infrastructure.Persistence.SaaSDbContext>();

        if (!await platformDb.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            platformDb.Tenants.Add(new SaaS.Core.Models.Tenant
            {
                Id = tenantId,
                Name = $"Tenant {tenantId:N}"[..18],
                Code = $"T_{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
                IsActive = true
            });
            await platformDb.SaveChangesAsync();
        }

        var config = await platformDb.TenantConfigurations.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (config == null)
        {
            config = SaaS.Core.Models.TenantConfiguration.CreateDefault(tenantId);
            config.UpdateSpecialUomConversion(enabled);
            await platformDb.TenantConfigurations.AddAsync(config);
        }
        else
        {
            config.UpdateSpecialUomConversion(enabled);
        }
        await platformDb.SaveChangesAsync();
    }

    private async Task SetCompanySpecialUomAsync(Guid tenantId, Guid companyId, bool enabled)
    {
        using var scope = _fixture.Services.CreateScope();
        var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var company = await domainDb.Companies.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId);
        if (company != null)
        {
            company.SpecialUomConversionEnabled = enabled;
            await domainDb.SaveChangesAsync();
        }
    }

    #endregion
}
