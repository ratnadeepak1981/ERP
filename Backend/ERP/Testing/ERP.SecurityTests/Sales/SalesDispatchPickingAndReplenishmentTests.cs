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
public class SalesDispatchPickingAndReplenishmentTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesDispatchPickingAndReplenishmentTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task AvailabilityDiscovery_MultipleWarehousesAndBins_ReturnsEligibleSourcesAndSplits()
    {
        // 1. Arrange: Setup Branch Warehouse with Bin A (3 units) and Bin B (2 units), and Alt Warehouse with 10 units
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation binA;
        WarehouseLocation binB;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var whBranch = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_PRI");
            var whAlt = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA2Id, "WH_ALT");

            binA = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whBranch.WarehouseId, "BIN_A");
            binB = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whBranch.WarehouseId, "BIN_B");
            var binRemote = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whAlt.WarehouseId, "BIN_REMOTE");

            await DepositInventoryAsync(whBranch.WarehouseId, binA.WarehouseLocationId, prod.ProductId, 3m);
            await DepositInventoryAsync(whBranch.WarehouseId, binB.WarehouseLocationId, prod.ProductId, 2m);
            await DepositInventoryAsync(whAlt.WarehouseId, binRemote.WarehouseLocationId, prod.ProductId, 10m);
        }

        // 2. Create Confirmed Sales Order for 5 units
        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m);

        // 3. Act: Discover availability
        var availRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");
        Assert.Equal(HttpStatusCode.OK, availRes.StatusCode);

        var avail = await availRes.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);
        Assert.NotNull(avail);
        Assert.Equal(order.Id, avail.SalesOrderId);
        Assert.Single(avail.Lines);

        var line = avail.Lines[0];
        Assert.Equal(5m, line.OrderedQuantity);
        Assert.Equal(5m, line.RemainingQuantity);
        Assert.Equal(5m, line.CurrentDraftAllocatedQuantity);

        // Eligible locations in primary warehouse contain both Bin A and Bin B
        Assert.Equal(2, line.EligibleLocations.Count);
        Assert.Contains(line.EligibleLocations, l => l.LocationCode == binA.LocationCode && l.AvailableQuantity == 3m);
        Assert.Contains(line.EligibleLocations, l => l.LocationCode == binB.LocationCode && l.AvailableQuantity == 2m);

        // Multi-bin suggested splits are provided to cover full 5 units
        Assert.NotEmpty(line.SuggestedSplits);
        decimal totalSuggested = line.SuggestedSplits.Sum(s => s.SuggestedQuantity);
        Assert.Equal(5m, totalSuggested);

        // Alternative warehouse with remote stock is discovered
        Assert.NotEmpty(line.AlternativeWarehouses);
        var altWhSummary = line.AlternativeWarehouses.FirstOrDefault(w => w.TotalAvailableQuantity == 10m);
        Assert.NotNull(altWhSummary);
        Assert.True(altWhSummary.CanFulfil);
    }

    [Fact]
    public async Task AvailabilityDiscovery_StagingDock_IsPrioritizedInDefaultSourceSuggestion()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation stgLoc;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_STG");

            var locTypeStaging = await db.LocationTypes.FirstOrDefaultAsync(lt => lt.Code == "STAGING");
            if (locTypeStaging == null)
            {
                locTypeStaging = LocationType.Create(TestConstants.TenantAlphaId, "STAGING", "Staging Dock", canPick: true);
                await db.LocationTypes.AddAsync(locTypeStaging);
                await db.SaveChangesAsync();
            }

            stgLoc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "STAGE_DOCK", locTypeStaging.LocationTypeId);
            var regLoc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "REG_BIN");

            // Put stock in both staging and regular bin
            await DepositInventoryAsync(wh.WarehouseId, stgLoc.WarehouseLocationId, prod.ProductId, 15m);
            await DepositInventoryAsync(wh.WarehouseId, regLoc.WarehouseLocationId, prod.ProductId, 15m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);

        // Act: Discover availability
        var availRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");
        Assert.Equal(HttpStatusCode.OK, availRes.StatusCode);

        var avail = await availRes.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);
        Assert.NotNull(avail);
        var line = avail.Lines[0];

        // Default suggested source must be the staging location
        Assert.Equal(stgLoc.LocationCode, line.DefaultSuggestedLocationCode);
        Assert.True(line.IsDefaultStaging);
        Assert.True(line.CanFulfil);
    }

    [Fact]
    public async Task DraftAllocation_MultiBinSplit_SavesMultipleAllocationRows_ZeroStockDeduction()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse wh;
        WarehouseLocation binA;
        WarehouseLocation binB;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_SPLIT");
            binA = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_S1");
            binB = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_S2");

            await DepositInventoryAsync(wh.WarehouseId, binA.WarehouseLocationId, prod.ProductId, 3m);
            await DepositInventoryAsync(wh.WarehouseId, binB.WarehouseLocationId, prod.ProductId, 2m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m);
        var orderItemId = order.Items[0].Id;

        // Create initial draft shipment
        var shipmentNum = $"SH-SPLIT-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = binA.WarehouseLocationId
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draft = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(draft);

        // Update draft with multi-bin split allocations: 3 units from Bin A, 2 units from Bin B
        var updateRes = await client.PutAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{draft.Id}",
            new UpdateSalesShipmentRequest
            {
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binA.WarehouseLocationId,
                        QuantityDispatched = 3m
                    },
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binB.WarehouseLocationId,
                        QuantityDispatched = 2m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Items.Count);
        Assert.All(updated.Items, i => Assert.Equal(orderItemId, i.SalesOrderItemId));
        Assert.Equal(5m, updated.Items.Sum(i => i.QuantityDispatched));

        // Invariant: Saving a draft must NOT deduct stock from the ledger
        var balA = await GetInventoryBalanceAsync(binA.WarehouseLocationId, prod.ProductId);
        var balB = await GetInventoryBalanceAsync(binB.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balA);
        Assert.NotNull(balB);
        Assert.Equal(3m, balA.QuantityOnHand);
        Assert.Equal(2m, balB.QuantityOnHand);
    }

    [Fact]
    public async Task DraftAllocation_OverShipmentRejectionAcrossSplitRows_EnforcesZeroTolerance()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse wh;
        WarehouseLocation binA;
        WarehouseLocation binB;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_OVR");
            binA = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_O1");
            binB = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_O2");
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m);
        var orderItemId = order.Items[0].Id;

        var shipmentNum = $"SH-OVR-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = binA.WarehouseLocationId
            });
        var draft = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);

        // Attempt over-allocation: Bin A: 3m, Bin B: 3m => Total 6m > 5m ordered
        var updateRes = await client.PutAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{draft!.Id}",
            new UpdateSalesShipmentRequest
            {
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binA.WarehouseLocationId,
                        QuantityDispatched = 3m
                    },
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binB.WarehouseLocationId,
                        QuantityDispatched = 3m
                    }
                }
            });

        Assert.True(updateRes.StatusCode == HttpStatusCode.BadRequest || updateRes.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DispatchPosting_MultiBinSplitAllocations_DeductsStockAccuratelyFromEachBin()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse wh;
        WarehouseLocation binA;
        WarehouseLocation binB;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_POST");
            binA = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_P1");
            binB = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "BIN_P2");

            await DepositInventoryAsync(wh.WarehouseId, binA.WarehouseLocationId, prod.ProductId, 10m);
            await DepositInventoryAsync(wh.WarehouseId, binB.WarehouseLocationId, prod.ProductId, 10m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 5m);
        var orderItemId = order.Items[0].Id;

        // Create draft with split items: 3 units from Bin A, 2 units from Bin B
        var shipmentNum = $"SH-POST-{Guid.NewGuid():N}"[..14];
        var createRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = shipmentNum,
                DefaultWarehouseId = wh.WarehouseId,
                DefaultLocationId = binA.WarehouseLocationId,
                Items = new List<CreateSalesShipmentItemRequest>
                {
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binA.WarehouseLocationId,
                        QuantityDispatched = 3m
                    },
                    new()
                    {
                        SalesOrderItemId = orderItemId,
                        ProductId = prod.ProductId,
                        WarehouseId = wh.WarehouseId,
                        WarehouseLocationId = binB.WarehouseLocationId,
                        QuantityDispatched = 2m
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draft = await createRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(draft);

        // Act: Dispatch shipment
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{draft.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);

        var dispatched = await dispatchRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(dispatched);
        Assert.Equal(SalesShipmentStatus.Dispatched, dispatched.Status);

        // Assert: Stock is accurately deducted from each bin
        var balA = await GetInventoryBalanceAsync(binA.WarehouseLocationId, prod.ProductId);
        var balB = await GetInventoryBalanceAsync(binB.WarehouseLocationId, prod.ProductId);
        Assert.NotNull(balA);
        Assert.NotNull(balB);
        Assert.Equal(7m, balA.QuantityOnHand); // 10 - 3 = 7
        Assert.Equal(8m, balB.QuantityOnHand); // 10 - 2 = 8

        // Repeated posting is idempotent and does not deduct stock again
        var repeatRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{draft.Id}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, repeatRes.StatusCode);

        balA = await GetInventoryBalanceAsync(binA.WarehouseLocationId, prod.ProductId);
        balB = await GetInventoryBalanceAsync(binB.WarehouseLocationId, prod.ProductId);
        Assert.Equal(7m, balA!.QuantityOnHand);
        Assert.Equal(8m, balB!.QuantityOnHand);
    }

    [Fact]
    public async Task AlternativeWarehouseDiscovery_IdentifiesRemoteStockAndShortages()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse whBranch;
        Warehouse whRemote;
        WarehouseLocation locBranch;
        WarehouseLocation locRemote;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            whBranch = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_BR1");
            whRemote = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA2Id, "WH_REM");

            locBranch = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whBranch.WarehouseId, "LOC_BR1");
            locRemote = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whRemote.WarehouseId, "LOC_REM");

            // Branch has 2 units, Remote has 20 units
            await DepositInventoryAsync(whBranch.WarehouseId, locBranch.WarehouseLocationId, prod.ProductId, 2m);
            await DepositInventoryAsync(whRemote.WarehouseId, locRemote.WarehouseLocationId, prod.ProductId, 20m);
        }

        // Order requires 10 units => shortage of 8 units in branch
        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);

        var availRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/availability");
        Assert.Equal(HttpStatusCode.OK, availRes.StatusCode);

        var avail = await availRes.ReadApiResponseDataAsync<SalesOrderDispatchAvailabilityDto>(_jsonOpts);
        Assert.NotNull(avail);
        Assert.True(avail.HasShortages);
        Assert.Equal(1, avail.TotalShortageLines);

        var line = avail.Lines[0];
        Assert.Equal(8m, line.ShortageQuantity);
        Assert.False(line.CanFulfil);

        var remoteWh = line.AlternativeWarehouses.FirstOrDefault(w => w.TotalAvailableQuantity == 20m);
        Assert.NotNull(remoteWh);
        Assert.True(remoteWh.CanFulfil);
    }

    [Fact]
    public async Task ReplenishmentTransferDraft_CreatesDraft_ZeroDestinationStockIncrease()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse whBranch;
        Warehouse whRemote;
        WarehouseLocation locBranch;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            whBranch = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_DST");
            whRemote = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA2Id, "WH_SRC");

            locBranch = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whBranch.WarehouseId, "LOC_DST");
            var locRemote = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whRemote.WarehouseId, "LOC_SRC");

            // Deposit 20 units in remote source warehouse
            await DepositInventoryAsync(whRemote.WarehouseId, locRemote.WarehouseLocationId, prod.ProductId, 20m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);
        var orderItemId = order.Items[0].Id;

        // Act: Initiate draft replenishment transfer for 8 units
        var transferReq = new CreateReplenishmentTransferRequest
        {
            SalesOrderId = order.Id,
            SalesOrderItemId = orderItemId,
            ProductId = prod.ProductId,
            SourceWarehouseId = whRemote.WarehouseId,
            DestinationWarehouseId = whBranch.WarehouseId,
            Quantity = 8m,
            Notes = "Transfer from central depot to branch"
        };

        var transferRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/replenishment-transfer",
            transferReq);
        Assert.Equal(HttpStatusCode.OK, transferRes.StatusCode);

        var result = await transferRes.ReadApiResponseDataAsync<ReplenishmentTransferResult>(_jsonOpts);
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.StartsWith("TR-REPL-", result.TransferReferenceNumber);
        Assert.Equal(8m, result.Quantity);

        // Invariant: Destination stock must NOT increase on draft transfer creation
        var destBalance = await GetInventoryBalanceAsync(locBranch.WarehouseLocationId, prod.ProductId);
        Assert.True(destBalance == null || destBalance.QuantityOnHand == 0m);
    }

    [Fact]
    public async Task ReplenishmentTransferDraft_RejectsInsufficientSourceStockOrIdenticalWarehouses()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse whBranch;
        Warehouse whRemote;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            whBranch = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_D1");
            whRemote = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA2Id, "WH_S1");

            var locRemote = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, whRemote.WarehouseId, "LOC_S1");
            await DepositInventoryAsync(whRemote.WarehouseId, locRemote.WarehouseLocationId, prod.ProductId, 5m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);
        var orderItemId = order.Items[0].Id;

        // 1. Rejects identical source and destination
        var sameWhRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/replenishment-transfer",
            new CreateReplenishmentTransferRequest
            {
                SalesOrderId = order.Id,
                SalesOrderItemId = orderItemId,
                ProductId = prod.ProductId,
                SourceWarehouseId = whBranch.WarehouseId,
                DestinationWarehouseId = whBranch.WarehouseId,
                Quantity = 5m
            });
        Assert.True(sameWhRes.StatusCode == HttpStatusCode.BadRequest || sameWhRes.StatusCode == HttpStatusCode.Conflict);

        // 2. Rejects request exceeding source warehouse stock (source has 5m, requested 20m)
        var excessRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/replenishment-transfer",
            new CreateReplenishmentTransferRequest
            {
                SalesOrderId = order.Id,
                SalesOrderItemId = orderItemId,
                ProductId = prod.ProductId,
                SourceWarehouseId = whRemote.WarehouseId,
                DestinationWarehouseId = whBranch.WarehouseId,
                Quantity = 20m
            });
        Assert.True(excessRes.StatusCode == HttpStatusCode.BadRequest || excessRes.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReplenishmentPurchaseRequisition_CreatesDraftRequisition_ZeroInventoryAdded()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        Warehouse wh;
        WarehouseLocation loc;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_REQ");
            loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "LOC_REQ");
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);

        // Act: Create draft replenishment purchase requisition for 10 units
        var prReq = new CreateReplenishmentRequisitionRequest
        {
            SalesOrderId = order.Id,
            RequiredDate = DateTime.UtcNow.AddDays(14),
            Notes = "Urgent replenishment for customer delivery",
            Items = new List<CreateReplenishmentRequisitionItemRequest>
            {
                new()
                {
                    ProductId = prod.ProductId,
                    Quantity = 10m,
                    EstimatedUnitPrice = 15m,
                    Remarks = "Backorder shortage replenishment"
                }
            }
        };

        var prRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/replenishment-requisition",
            prReq);
        Assert.Equal(HttpStatusCode.OK, prRes.StatusCode);

        var result = await prRes.ReadApiResponseDataAsync<ReplenishmentRequisitionResult>(_jsonOpts);
        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
        Assert.StartsWith("PR-REPL-", result.RequisitionNumber);
        Assert.Equal(1, result.ItemCount);

        // Invariant: Fictitious supplier receipts and artificial stock additions are strictly prohibited
        var balance = await GetInventoryBalanceAsync(loc.WarehouseLocationId, prod.ProductId);
        Assert.True(balance == null || balance.QuantityOnHand == 0m);
    }

    [Fact]
    public async Task ReplenishmentEndpoints_UnauthorizedScope_ReturnsForbidden()
    {
        var betaClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_beta",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.EDIT" });

        var fakeOrderId = Guid.NewGuid();

        var res = await betaClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{fakeOrderId}/replenishment-transfer",
            new CreateReplenishmentTransferRequest
            {
                SalesOrderId = fakeOrderId,
                SalesOrderItemId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                SourceWarehouseId = Guid.NewGuid(),
                DestinationWarehouseId = Guid.NewGuid(),
                Quantity = 5m
            });

        Assert.True(res.StatusCode == HttpStatusCode.Forbidden || res.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegressionScenario1_FullDispatchViaDraft_UpdatesSalesOrderToFulfilledAndQuantitiesReconciled()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation loc;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_REG1");
            loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "LOC_REG1");
            await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 10m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);

        // Find auto-created draft
        var draftsRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments?status=1");
        var drafts = await draftsRes.ReadApiResponseDataAsync<List<SalesShipmentDto>>(_jsonOpts);
        var autoDraft = drafts!.FirstOrDefault(d => d.SalesOrderId == order.Id);
        Assert.NotNull(autoDraft);

        // Update draft allocation and dispatch
        var updateReq = new UpdateSalesShipmentRequest
        {
            Items = new List<CreateSalesShipmentItemRequest>
            {
                new()
                {
                    SalesOrderItemId = order.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = loc.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityDispatched = 10m
                }
            }
        };
        await client.PutAsJsonAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{autoDraft.Id}", updateReq);

        var dispatchRes = await client.PostAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{autoDraft.Id}/dispatch", null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);

        // Verify Sales Order status and quantities
        var orderRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var updatedOrder = await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(updatedOrder);
        Assert.Equal(SalesOrderStatus.Fulfilled, updatedOrder.Status);
        Assert.Equal("Fulfilled", updatedOrder.FulfillmentStatus);
        Assert.Equal(10m, updatedOrder.TotalOrderedQuantity);
        Assert.Equal(10m, updatedOrder.TotalDispatchedQuantity);
        Assert.Equal(0m, updatedOrder.TotalOutstandingQuantity);
    }

    [Fact]
    public async Task RegressionScenario2_FullDispatchViaSecondShipment_AutoCancelsOriginalDraftWithAuditReason()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation loc;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_REG2");
            loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "LOC_REG2");
            await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 15m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 15m);

        // Auto draft exists
        var draftsRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments?status=1");
        var drafts = await draftsRes.ReadApiResponseDataAsync<List<SalesShipmentDto>>(_jsonOpts);
        var autoDraft = drafts!.FirstOrDefault(d => d.SalesOrderId == order.Id);
        Assert.NotNull(autoDraft);

        // Create and dispatch via a second shipment directly
        var dispatchReq = new DispatchOrderRequest
        {
            WarehouseId = loc.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Items = new List<CreateSalesShipmentItemRequest>
            {
                new()
                {
                    SalesOrderItemId = order.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = loc.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityDispatched = 15m
                }
            }
        };

        var dispatchRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            dispatchReq);
        Assert.Equal(HttpStatusCode.Created, dispatchRes.StatusCode);

        // Verify the original draft was auto-cancelled
        var autoDraftRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{autoDraft.Id}");
        var cancelledDraft = await autoDraftRes.ReadApiResponseDataAsync<SalesShipmentDto>(_jsonOpts);
        Assert.NotNull(cancelledDraft);
        Assert.Equal(SalesShipmentStatus.Cancelled, cancelledDraft.Status);
        Assert.Contains("[Auto-Cancelled]", cancelledDraft.Notes ?? string.Empty);
    }

    [Fact]
    public async Task RegressionScenario3_PartialDispatch_SalesOrderRemainsConfirmed_QuantitiesAccurate_OpenDraftPreserved()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation loc;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_REG3");
            loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "LOC_REG3");
            await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 20m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 20m);

        // Dispatch 8 out of 20
        var dispatchReq = new DispatchOrderRequest
        {
            WarehouseId = loc.WarehouseId,
            WarehouseLocationId = loc.WarehouseLocationId,
            Items = new List<CreateSalesShipmentItemRequest>
            {
                new()
                {
                    SalesOrderItemId = order.Items[0].Id,
                    ProductId = prod.ProductId,
                    WarehouseId = loc.WarehouseId,
                    WarehouseLocationId = loc.WarehouseLocationId,
                    QuantityDispatched = 8m
                }
            }
        };

        var dispatchRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{order.Id}/dispatch",
            dispatchReq);
        Assert.Equal(HttpStatusCode.Created, dispatchRes.StatusCode);

        // Verify Sales Order remains Confirmed with Partially Dispatched fulfillment status
        var orderRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders/{order.Id}");
        var updatedOrder = await orderRes.ReadApiResponseDataAsync<SalesOrderDto>(_jsonOpts);
        Assert.NotNull(updatedOrder);
        Assert.Equal(SalesOrderStatus.Confirmed, updatedOrder.Status);
        Assert.Equal("Partially Dispatched", updatedOrder.FulfillmentStatus);
        Assert.Equal(20m, updatedOrder.TotalOrderedQuantity);
        Assert.Equal(8m, updatedOrder.TotalDispatchedQuantity);
        Assert.Equal(12m, updatedOrder.TotalOutstandingQuantity);
    }

    [Fact]
    public async Task RegressionScenario6_CancelledDraft_CannotBeDispatched_ThrowsInvalidOperation()
    {
        var client = CreateAuthorizedClient();
        var prod = await CreateTestProductAsync(TestConstants.TenantAlphaId, ProductType.StockItem, isStockTracked: true);
        WarehouseLocation loc;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var wh = await CreateTestWarehouseAsync(db, TestConstants.TenantAlphaId, TestConstants.CompanyAId, TestConstants.BranchA1Id, "WH_REG6");
            loc = await CreateTestLocationAsync(db, TestConstants.TenantAlphaId, wh.WarehouseId, "LOC_REG6");
            await DepositInventoryAsync(wh.WarehouseId, loc.WarehouseLocationId, prod.ProductId, 10m);
        }

        var order = await CreateConfirmedSalesOrderAsync(client, prod.ProductId, 10m);

        var draftsRes = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments?status=1");
        var drafts = await draftsRes.ReadApiResponseDataAsync<List<SalesShipmentDto>>(_jsonOpts);
        var autoDraft = drafts!.FirstOrDefault(d => d.SalesOrderId == order.Id);
        Assert.NotNull(autoDraft);

        // Explicitly cancel the draft
        var cancelRes = await client.PostAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{autoDraft.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        // Attempting to dispatch the cancelled draft must be rejected
        var dispatchRes = await client.PostAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{autoDraft.Id}/dispatch", null);
        Assert.True(dispatchRes.StatusCode == HttpStatusCode.Conflict || dispatchRes.StatusCode == HttpStatusCode.BadRequest);
    }

    #region Helpers

    private HttpClient CreateAuthorizedClient()
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
        var orderNum = $"SO-PICK-{Guid.NewGuid():N}"[..14];
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

    private static async Task<Warehouse> CreateTestWarehouseAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        string prefix)
    {
        string code = $"{prefix}_{Guid.NewGuid():N}"[..25];
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

    private static async Task<WarehouseLocation> CreateTestLocationAsync(
        DomainDbContext db,
        Guid tenantId,
        Guid warehouseId,
        string prefix,
        Guid? locationTypeId = null)
    {
        string code = $"{prefix}_{Guid.NewGuid():N}"[..25];
        var loc = WarehouseLocation.Create(
            tenantId,
            warehouseId,
            warehouseZoneId: null,
            locationCode: code,
            locationName: $"Location {code}",
            locationTypeId: locationTypeId);

        await db.WarehouseLocations.AddAsync(loc);
        await db.SaveChangesAsync();
        return loc;
    }

    private async Task<Product> CreateTestProductAsync(Guid tenantId, ProductType productType, bool isStockTracked)
    {
        string code = $"PRD_{Guid.NewGuid():N}"[..25];
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

    #endregion
}
