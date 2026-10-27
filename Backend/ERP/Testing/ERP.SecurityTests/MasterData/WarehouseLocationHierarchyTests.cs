using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Inventory.Putaway;
using Domain.Features.MasterData.Product;
using Domain.Features.Procurement.GoodsReceiptNote;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Sales.SalesOrder;
using Domain.Features.Sales.Shipment;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.Inventory;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.MasterData;

public class WarehouseLocationHierarchyTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public WarehouseLocationHierarchyTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Warehouse_SupportsZonedAndUnzonedLocations_AndMultiLevelStorageHierarchy()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.MANAGE_ZONES", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.UPDATE", "LOCATION.DELETE" });

        // 1. Create warehouse
        string whCode = $"WH_{Guid.NewGuid():N}".Substring(0, 8);
        string whName = $"Main Logistics Hub_{Guid.NewGuid():N}";
        var whRes = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = whName,
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create Location Types: Grouping position (Area / Aisle) and Stock-holding position (Bin)
        var areaTypeRes = await clientA.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"AREA_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Storage Area",
            CanStoreInventory = false,
            CanPick = false,
            CanPutAway = false,
            CanContainChildren = true
        });
        Assert.Equal(HttpStatusCode.OK, areaTypeRes.StatusCode);
        var areaType = await areaTypeRes.ReadDataJsonElementAsync();
        var areaTypeId = areaType.GetProperty("locationTypeId").GetGuid();

        var binTypeRes = await clientA.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"BIN_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Storage Bin",
            CanStoreInventory = true,
            CanPick = true,
            CanPutAway = true,
            CanContainChildren = false
        });
        Assert.Equal(HttpStatusCode.OK, binTypeRes.StatusCode);
        var binType = await binTypeRes.ReadDataJsonElementAsync();
        var binTypeId = binType.GetProperty("locationTypeId").GetGuid();

        // 3. Create unzoned root location (e.g., General Receiving Bay) directly under warehouse
        var unzonedLocRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BAY_UNZONED",
            LocationName = "Direct Warehouse Bay",
            LocationTypeId = areaTypeId
        });
        Assert.Equal(HttpStatusCode.OK, unzonedLocRes.StatusCode);
        var unzonedLoc = await unzonedLocRes.ReadDataJsonElementAsync();
        var unzonedLocId = unzonedLoc.GetProperty("warehouseLocationId").GetGuid();
        Assert.True(unzonedLoc.GetProperty("warehouseZoneId").ValueKind == JsonValueKind.Null || unzonedLoc.GetProperty("warehouseZoneId").GetString() == null);

        // 4. Create child under unzoned root location
        var childUnzonedRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BAY_BIN_01",
            LocationName = "Bay Bin 01",
            LocationTypeId = binTypeId,
            ParentLocationId = unzonedLocId
        });
        Assert.Equal(HttpStatusCode.OK, childUnzonedRes.StatusCode);
        var childUnzoned = await childUnzonedRes.ReadDataJsonElementAsync();
        Assert.Equal(unzonedLocId, childUnzoned.GetProperty("parentLocationId").GetGuid());

        // 5. Create Zone in warehouse
        var zoneRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZONE_COLD",
            ZoneName = "Cold Storage Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 6. Create multi-level zoned storage hierarchy: Zone -> Aisle (Area) -> Rack -> Shelf -> Bin
        // Level 1: Aisle
        var aisleRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "COLD_AISLE_1",
            LocationName = "Cold Aisle 1",
            LocationTypeId = areaTypeId
        });
        Assert.Equal(HttpStatusCode.OK, aisleRes.StatusCode);
        var aisle = await aisleRes.ReadDataJsonElementAsync();
        var aisleId = aisle.GetProperty("warehouseLocationId").GetGuid();
        Assert.Equal(zoneId, aisle.GetProperty("warehouseZoneId").GetGuid());

        // Level 2: Rack under Aisle
        var rackRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "COLD_RACK_1",
            LocationName = "Cold Rack 1",
            LocationTypeId = areaTypeId,
            ParentLocationId = aisleId
        });
        Assert.Equal(HttpStatusCode.OK, rackRes.StatusCode);
        var rack = await rackRes.ReadDataJsonElementAsync();
        var rackId = rack.GetProperty("warehouseLocationId").GetGuid();

        // Level 3: Shelf under Rack
        var shelfRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "COLD_SHELF_1",
            LocationName = "Cold Shelf 1",
            LocationTypeId = areaTypeId,
            ParentLocationId = rackId
        });
        Assert.Equal(HttpStatusCode.OK, shelfRes.StatusCode);
        var shelf = await shelfRes.ReadDataJsonElementAsync();
        var shelfId = shelf.GetProperty("warehouseLocationId").GetGuid();

        // Level 4: Bin under Shelf (stock holding)
        var binRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "COLD_BIN_101",
            LocationName = "Cold Bin 101",
            LocationTypeId = binTypeId,
            ParentLocationId = shelfId
        });
        Assert.Equal(HttpStatusCode.OK, binRes.StatusCode);
        var bin = await binRes.ReadDataJsonElementAsync();
        var binId = bin.GetProperty("warehouseLocationId").GetGuid();
        Assert.Equal(shelfId, bin.GetProperty("parentLocationId").GetGuid());

        // 7. Verify safe structural deactivation guard: Cannot deactivate shelf while bin is active child
        var deactShelfRes = await clientA.DeleteAsync($"/api/warehouses/locations/{shelfId}");
        Assert.Equal(HttpStatusCode.Conflict, deactShelfRes.StatusCode);

        // Deactivating the leaf child (bin) succeeds
        var deactBinRes = await clientA.DeleteAsync($"/api/warehouses/locations/{binId}");
        Assert.Equal(HttpStatusCode.NoContent, deactBinRes.StatusCode);

        // Now shelf can be deactivated
        var deactShelfSuccessRes = await clientA.DeleteAsync($"/api/warehouses/locations/{shelfId}");
        Assert.Equal(HttpStatusCode.NoContent, deactShelfSuccessRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseLocation_HierarchySafety_CrossWarehouseAndDifferentZoneValidation()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.MANAGE_ZONES", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.UPDATE" });

        // Warehouse 1
        var wh1Res = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"WH1_{Guid.NewGuid():N}".Substring(0, 8),
            WarehouseName = $"Warehouse 1_{Guid.NewGuid():N}",
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, wh1Res.StatusCode);
        var wh1 = await wh1Res.ReadDataJsonElementAsync();
        var wh1Id = wh1.GetProperty("warehouseId").GetGuid();

        // Warehouse 2
        var wh2Res = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"WH2_{Guid.NewGuid():N}".Substring(0, 8),
            WarehouseName = $"Warehouse 2_{Guid.NewGuid():N}",
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, wh2Res.StatusCode);
        var wh2 = await wh2Res.ReadDataJsonElementAsync();
        var wh2Id = wh2.GetProperty("warehouseId").GetGuid();

        // Location in Warehouse 1
        var loc1Res = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "WH1_LOC_A",
            LocationName = "WH1 Location A"
        });
        var loc1 = await loc1Res.ReadDataJsonElementAsync();
        var loc1Id = loc1.GetProperty("warehouseLocationId").GetGuid();

        // Attempt to create location in Warehouse 2 with Parent from Warehouse 1 -> Rejected
        var crossWhRes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh2Id}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "WH2_LOC_B",
            LocationName = "WH2 Location B",
            ParentLocationId = loc1Id
        });
        Assert.Equal(HttpStatusCode.Conflict, crossWhRes.StatusCode);

        // Within Warehouse 1: Create Zone A and Zone B
        var zoneARes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZA",
            ZoneName = "Zone A"
        });
        var zoneA = await zoneARes.ReadDataJsonElementAsync();
        var zoneAId = zoneA.GetProperty("warehouseZoneId").GetGuid();

        var zoneBRes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZB",
            ZoneName = "Zone B"
        });
        var zoneB = await zoneBRes.ReadDataJsonElementAsync();
        var zoneBId = zoneB.GetProperty("warehouseZoneId").GetGuid();

        // Location in Zone A
        var locZoneARes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones/{zoneAId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "ZA_LOC_1",
            LocationName = "Zone A Location 1"
        });
        var locZoneA = await locZoneARes.ReadDataJsonElementAsync();
        var locZoneAId = locZoneA.GetProperty("warehouseLocationId").GetGuid();

        // Location in Zone B with Parent in Zone A -> Rejected (different zones)
        var diffZoneRes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones/{zoneBId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "ZB_LOC_1",
            LocationName = "Zone B Location 1",
            ParentLocationId = locZoneAId
        });
        Assert.Equal(HttpStatusCode.Conflict, diffZoneRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseLocation_TenantIsolation_CrossTenantAccessIsDenied()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "LOCATION.VIEW", "LOCATION.CREATE" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "LOCATION.VIEW", "LOCATION.CREATE" });

        // Tenant A creates warehouse and location
        var whRes = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W1_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Tenant 1 Warehouse_{Guid.NewGuid():N}",
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        var locRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = $"LOC_{Guid.NewGuid():N}"[..10],
            LocationName = "Secret Location"
        });
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
        var loc = await locRes.ReadDataJsonElementAsync();
        var locId = loc.GetProperty("warehouseLocationId").GetGuid();

        // Tenant B attempts to get Tenant A warehouse locations -> Returns empty list
        var tenantBGetRes = await clientB.GetAsync($"/api/warehouses/{whId}/locations");
        Assert.Equal(HttpStatusCode.OK, tenantBGetRes.StatusCode);
        var tenantBLocs = await tenantBGetRes.ReadApiResponseDataAsync<List<JsonElement>>();
        Assert.Empty(tenantBLocs!);

        // Tenant B attempts to create child location under Tenant A location in their own warehouse -> Rejected
        var whBRes = await clientB.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W2_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Tenant 2 Warehouse_{Guid.NewGuid():N}",
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, whBRes.StatusCode);
        var whB = await whBRes.ReadDataJsonElementAsync();
        var whBId = whB.GetProperty("warehouseId").GetGuid();

        var crossTenantParentRes = await clientB.PostAsJsonAsync($"/api/warehouses/{whBId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "TB_ATTEMPT",
            LocationName = "Tenant B Attempt",
            ParentLocationId = locId
        });
        Assert.Equal(HttpStatusCode.Conflict, crossTenantParentRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseAndZone_AuthorizedOperations_Succeed()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES" });

        // 1. Create warehouse
        var whCode = $"WH_{Guid.NewGuid():N}"[..12];
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Primary Center_{Guid.NewGuid():N}"[..22],
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Update warehouse
        var whUpdateRes = await client.PutAsJsonAsync($"/api/warehouses/{whId}", new UpdateWarehouseRequest
        {
            WarehouseName = $"Primary Center Upd_{Guid.NewGuid():N}"[..22],
            City = "Colombo South"
        });
        Assert.Equal(HttpStatusCode.OK, whUpdateRes.StatusCode);

        // 3. Create Zone
        var zoneCode = $"Z_{Guid.NewGuid():N}"[..8];
        var zoneRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = zoneCode,
            ZoneName = "High Value Zone",
            Description = "Secure Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 4. Update Zone via nested route
        var zoneUpdateRes = await client.PutAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}", new UpdateWarehouseZoneRequest
        {
            ZoneName = "High Value Cage Updated",
            Description = "Secure Enclosure"
        });
        Assert.Equal(HttpStatusCode.OK, zoneUpdateRes.StatusCode);
        var updatedZone = await zoneUpdateRes.ReadDataJsonElementAsync();
        Assert.Equal("High Value Cage Updated", updatedZone.GetProperty("zoneName").GetString());

        // 5. Update Zone via direct route
        var zoneUpdateDirectRes = await client.PutAsJsonAsync($"/api/warehouses/zones/{zoneId}", new UpdateWarehouseZoneRequest
        {
            ZoneName = "High Value Vault",
            Description = "Biometric Enclosure"
        });
        Assert.Equal(HttpStatusCode.OK, zoneUpdateDirectRes.StatusCode);

        // 6. View single zone
        var getZoneRes = await client.GetAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.OK, getZoneRes.StatusCode);
        var fetchedZone = await getZoneRes.ReadDataJsonElementAsync();
        Assert.Equal("High Value Vault", fetchedZone.GetProperty("zoneName").GetString());

        // 7. List zones
        var listZonesRes = await client.GetAsync($"/api/warehouses/{whId}/zones");
        Assert.Equal(HttpStatusCode.OK, listZonesRes.StatusCode);
        var zonesList = await listZonesRes.ReadApiResponseDataAsync<List<JsonElement>>();
        Assert.Contains(zonesList!, z => z.GetProperty("warehouseZoneId").GetGuid() == zoneId);

        // 8. Deactivate Zone
        var deactZoneRes = await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.NoContent, deactZoneRes.StatusCode);

        // 9. Deactivate Warehouse
        var deactWhRes = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.NoContent, deactWhRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseAndZone_MissingOrInsufficientPermissions_ReturnsForbidden()
    {
        // Read-only user with only WAREHOUSE.VIEW
        var readOnlyClient = _fixture.CreateAuthenticatedClient(
            Guid.NewGuid(),
            "readonly_user",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "WAREHOUSE.VIEW" });

        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES" });

        // Admin creates a warehouse and zone for test fixtures
        var whCode = $"WH_{Guid.NewGuid():N}"[..12];
        var whRes = await adminClient.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Perm WH_{Guid.NewGuid():N}"[..20],
            City = "Galle"
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        var zoneRes = await adminClient.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = $"Z_{Guid.NewGuid():N}"[..8],
            ZoneName = "Perm Zone"
        });
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 1. Read-only user attempting to create warehouse -> 403
        var createWhRes = await readOnlyClient.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W_FAIL_{Guid.NewGuid():N}"[..12],
            WarehouseName = "Unauthorized WH",
            City = "Galle"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createWhRes.StatusCode);

        // 2. Read-only user attempting to update warehouse -> 403
        var updateWhRes = await readOnlyClient.PutAsJsonAsync($"/api/warehouses/{whId}", new UpdateWarehouseRequest
        {
            WarehouseName = "Hacked Name"
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateWhRes.StatusCode);

        // 3. Read-only user attempting to create zone -> 403
        var createZoneRes = await readOnlyClient.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "Z_FAIL",
            ZoneName = "Unauthorized Zone"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createZoneRes.StatusCode);

        // 4. Read-only user attempting to update zone -> 403
        var updateZoneRes = await readOnlyClient.PutAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}", new UpdateWarehouseZoneRequest
        {
            ZoneName = "Hacked Zone Name"
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateZoneRes.StatusCode);

        // 5. Read-only user attempting to deactivate zone -> 403
        var deactZoneRes = await readOnlyClient.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.Forbidden, deactZoneRes.StatusCode);

        // 6. Read-only user attempting to deactivate warehouse -> 403
        var deactWhRes = await readOnlyClient.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.Forbidden, deactWhRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseAndZone_TenantIsolation_CrossTenantAccessIsDenied()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES" });

        // Tenant A creates warehouse and zone
        var whRes = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W_ISO_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Tenant A WH_{Guid.NewGuid():N}"[..22],
            City = "Colombo"
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        var zoneRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = $"ZA_{Guid.NewGuid():N}"[..8],
            ZoneName = "Tenant A Secret Zone"
        });
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // Tenant B attempts to get Tenant A warehouse -> 404
        var getWhRes = await clientB.GetAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.NotFound, getWhRes.StatusCode);

        // Tenant B attempts to update Tenant A warehouse -> 404
        var updateWhRes = await clientB.PutAsJsonAsync($"/api/warehouses/{whId}", new UpdateWarehouseRequest
        {
            WarehouseName = "Breached Warehouse"
        });
        Assert.Equal(HttpStatusCode.NotFound, updateWhRes.StatusCode);

        // Tenant B attempts to get Tenant A zone -> 404
        var getZoneRes = await clientB.GetAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.NotFound, getZoneRes.StatusCode);

        // Tenant B attempts to update Tenant A zone -> 404
        var updateZoneRes = await clientB.PutAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}", new UpdateWarehouseZoneRequest
        {
            ZoneName = "Breached Zone"
        });
        Assert.Equal(HttpStatusCode.NotFound, updateZoneRes.StatusCode);

        // Tenant B attempts to deactivate Tenant A zone -> 404
        var deactZoneRes = await clientB.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.NotFound, deactZoneRes.StatusCode);

        // Tenant B attempts to create zone under Tenant A warehouse -> 404
        var createZoneInWhARes = await clientB.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "Z_B_IN_A",
            ZoneName = "Intruder Zone"
        });
        Assert.Equal(HttpStatusCode.NotFound, createZoneInWhARes.StatusCode);

        // Tenant B attempts to list zones for Tenant A warehouse -> returns empty list
        var listZonesRes = await clientB.GetAsync($"/api/warehouses/{whId}/zones");
        Assert.Equal(HttpStatusCode.OK, listZonesRes.StatusCode);
        var zones = await listZonesRes.ReadApiResponseDataAsync<List<JsonElement>>();
        Assert.Empty(zones!);
    }

    [Fact]
    public async Task WarehouseZone_WrongWarehouseId_ReturnsConflict()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES" });

        // Warehouse 1 and Zone 1
        var wh1Res = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W1_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Warehouse 1_{Guid.NewGuid():N}"[..22],
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, wh1Res.StatusCode);
        var wh1 = await wh1Res.ReadDataJsonElementAsync();
        var wh1Id = wh1.GetProperty("warehouseId").GetGuid();

        var zone1Res = await client.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = $"Z1_{Guid.NewGuid():N}"[..8],
            ZoneName = "Warehouse 1 Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zone1Res.StatusCode);
        var zone1 = await zone1Res.ReadDataJsonElementAsync();
        var zone1Id = zone1.GetProperty("warehouseZoneId").GetGuid();

        // Warehouse 2
        var wh2Res = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W2_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Warehouse 2_{Guid.NewGuid():N}"[..22],
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, wh2Res.StatusCode);
        var wh2 = await wh2Res.ReadDataJsonElementAsync();
        var wh2Id = wh2.GetProperty("warehouseId").GetGuid();

        // Update Zone 1 providing Warehouse 2 in route -> Conflict
        var updateWrongWhRes = await client.PutAsJsonAsync($"/api/warehouses/{wh2Id}/zones/{zone1Id}", new UpdateWarehouseZoneRequest
        {
            ZoneName = "Illegal Reassigned Zone"
        });
        Assert.Equal(HttpStatusCode.Conflict, updateWrongWhRes.StatusCode);

        // Deactivate Zone 1 providing Warehouse 2 in route -> Conflict
        var deactWrongWhRes = await client.DeleteAsync($"/api/warehouses/{wh2Id}/zones/{zone1Id}");
        Assert.Equal(HttpStatusCode.Conflict, deactWrongWhRes.StatusCode);

        // Get Zone 1 providing Warehouse 2 in route -> Conflict
        var getWrongWhRes = await client.GetAsync($"/api/warehouses/{wh2Id}/zones/{zone1Id}");
        Assert.Equal(HttpStatusCode.Conflict, getWrongWhRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseAndZone_DeactivationWithDependentRecords_IsPrevented()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.MANAGE_ZONES", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.DELETE" });

        // 1. Create warehouse
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W_DEP_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Dep Guard WH_{Guid.NewGuid():N}"[..22],
            City = "Negombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create zone in warehouse
        var zoneRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = $"ZD_{Guid.NewGuid():N}"[..8],
            ZoneName = "Dependency Guard Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 3. Create active storage location in this zone
        var locRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = $"LD_{Guid.NewGuid():N}"[..8],
            LocationName = "Dependency Location"
        });
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
        var loc = await locRes.ReadDataJsonElementAsync();
        var locId = loc.GetProperty("warehouseLocationId").GetGuid();

        // 4. Attempt to deactivate zone while active location exists -> Conflict
        var deactZoneBlockedRes = await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.Conflict, deactZoneBlockedRes.StatusCode);

        // 5. Attempt to deactivate warehouse while active zone & location exist -> Conflict
        var deactWhBlockedRes = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.Conflict, deactWhBlockedRes.StatusCode);

        // 6. Deactivate the child location first -> 204 NoContent
        var deactLocRes = await client.DeleteAsync($"/api/warehouses/locations/{locId}");
        Assert.Equal(HttpStatusCode.NoContent, deactLocRes.StatusCode);

        // 7. Deactivating zone now succeeds -> 204 NoContent
        var deactZoneSuccessRes = await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.NoContent, deactZoneSuccessRes.StatusCode);

        // 8. Deactivating warehouse now succeeds -> 204 NoContent
        var deactWhSuccessRes = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.NoContent, deactWhSuccessRes.StatusCode);
    }

    [Fact]
    public async Task WarehouseLocation_PhysicalHierarchyTypeValidation_EnforcesRackShelfBinRules()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.MANAGE_ZONES", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.UPDATE", "LOCATION.DELETE" });

        // 1. Create warehouse
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"W_HR_{Guid.NewGuid():N}"[..12],
            WarehouseName = $"Hierarchy WH_{Guid.NewGuid():N}"[..22],
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create standard location types: RACK, SHELF, BIN
        var rackTypeRes = await client.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"RACK_{Guid.NewGuid():N}"[..8],
            Name = "Storage Rack",
            CanStoreInventory = false,
            CanPick = false,
            CanPutAway = false,
            CanContainChildren = true
        });
        Assert.Equal(HttpStatusCode.OK, rackTypeRes.StatusCode);
        var rackType = await rackTypeRes.ReadDataJsonElementAsync();
        var rackTypeId = rackType.GetProperty("locationTypeId").GetGuid();

        var shelfTypeRes = await client.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"SHELF_{Guid.NewGuid():N}"[..8],
            Name = "Storage Shelf",
            CanStoreInventory = false,
            CanPick = false,
            CanPutAway = false,
            CanContainChildren = true
        });
        Assert.Equal(HttpStatusCode.OK, shelfTypeRes.StatusCode);
        var shelfType = await shelfTypeRes.ReadDataJsonElementAsync();
        var shelfTypeId = shelfType.GetProperty("locationTypeId").GetGuid();

        var binTypeRes = await client.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"BIN_{Guid.NewGuid():N}"[..8],
            Name = "Storage Bin",
            CanStoreInventory = true,
            CanPick = true,
            CanPutAway = true,
            CanContainChildren = false
        });
        Assert.Equal(HttpStatusCode.OK, binTypeRes.StatusCode);
        var binType = await binTypeRes.ReadDataJsonElementAsync();
        var binTypeId = binType.GetProperty("locationTypeId").GetGuid();

        // 3. Create RACK (top-level, no parent) -> succeeds
        var rackRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "RACK_01",
            LocationName = "General Rack 01",
            LocationTypeId = rackTypeId
        });
        Assert.Equal(HttpStatusCode.OK, rackRes.StatusCode);
        var rack = await rackRes.ReadDataJsonElementAsync();
        var rackId = rack.GetProperty("warehouseLocationId").GetGuid();

        // 4. Create SHELF under RACK -> succeeds
        var shelfRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "SHELF_01",
            LocationName = "Rack 01 Shelf 01",
            LocationTypeId = shelfTypeId,
            ParentLocationId = rackId
        });
        Assert.Equal(HttpStatusCode.OK, shelfRes.StatusCode);
        var shelf = await shelfRes.ReadDataJsonElementAsync();
        var shelfId = shelf.GetProperty("warehouseLocationId").GetGuid();

        // 5. Create BIN under SHELF -> succeeds
        var binRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BIN_01",
            LocationName = "Shelf 01 Bin 01",
            LocationTypeId = binTypeId,
            ParentLocationId = shelfId
        });
        Assert.Equal(HttpStatusCode.OK, binRes.StatusCode);
        var bin = await binRes.ReadDataJsonElementAsync();
        var binId = bin.GetProperty("warehouseLocationId").GetGuid();

        // 6. Attempt to create a child under BIN (which has CanContainChildren=false) -> 409 Conflict
        var invalidChildRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BIN_CHILD_INVALID",
            LocationName = "Invalid Child Under Bin",
            LocationTypeId = binTypeId,
            ParentLocationId = binId
        });
        Assert.Equal(HttpStatusCode.Conflict, invalidChildRes.StatusCode);

        // 7. Deactivate SHELF, then attempt to create new child under inactive SHELF -> 409 Conflict
        // First delete BIN so shelf can be deactivated
        var delBinRes = await client.DeleteAsync($"/api/warehouses/locations/{binId}");
        Assert.Equal(HttpStatusCode.NoContent, delBinRes.StatusCode);

        var deactShelfRes = await client.DeleteAsync($"/api/warehouses/locations/{shelfId}");
        Assert.Equal(HttpStatusCode.NoContent, deactShelfRes.StatusCode);

        var childUnderInactiveRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BIN_UNDER_INACTIVE",
            LocationName = "Bin Under Inactive Shelf",
            LocationTypeId = binTypeId,
            ParentLocationId = shelfId
        });
        Assert.Equal(HttpStatusCode.Conflict, childUnderInactiveRes.StatusCode);
    }

    [Fact]
    public async Task Deactivation_Blocked_WhenStockBalancesRemain_InLocation_OrParent_OrZone_OrWarehouse()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.DEACTIVATE", "WAREHOUSE.ACTIVATE", "WAREHOUSE.MANAGE_ZONES",
                "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.DELETE", "LOCATION.DEACTIVATE", "LOCATION.ACTIVATE"
            });

        // 1. Create warehouse
        string whCode = $"WH_{Guid.NewGuid():N}"[..8];
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create zone
        string zoneCode = $"ZN_{Guid.NewGuid():N}"[..8];
        var zoneRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = zoneCode,
            ZoneName = $"Zone {zoneCode}"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 3. Create area location (under zone)
        string areaCode = $"AREA_{Guid.NewGuid():N}"[..8];
        var areaRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = areaCode,
            LocationName = $"Area {areaCode}",
            WarehouseZoneId = zoneId
        });
        Assert.Equal(HttpStatusCode.OK, areaRes.StatusCode);
        var area = await areaRes.ReadDataJsonElementAsync();
        var areaId = area.GetProperty("warehouseLocationId").GetGuid();

        // 4. Create bin location (under area)
        string binCode = $"BIN_{Guid.NewGuid():N}"[..8];
        var binRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = binCode,
            LocationName = $"Bin {binCode}",
            WarehouseZoneId = zoneId,
            ParentLocationId = areaId
        });
        Assert.Equal(HttpStatusCode.OK, binRes.StatusCode);
        var bin = await binRes.ReadDataJsonElementAsync();
        var binId = bin.GetProperty("warehouseLocationId").GetGuid();

        // 5. Seed stock balance directly in DB
        Guid productId;
        Guid balanceId = Guid.NewGuid();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var prod = Product.Create(
                tenantId: TestConstants.TenantAlphaId,
                productCode: $"PB_{Guid.NewGuid():N}"[..8],
                productName: $"Balance Item {Guid.NewGuid():N}"[..20],
                categoryId: TestConstants.CategoryAlphaId,
                unitOfMeasure: "PCS",
                description: null,
                isStockTracked: true,
                productType: ProductType.StockItem);
            await db.Products.AddAsync(prod);
            await db.SaveChangesAsync();
            productId = prod.ProductId;

            var bal = InventoryBalance.Create(
                TestConstants.TenantAlphaId,
                whId,
                binId,
                productId,
                inventoryBalanceId: balanceId);
            bal.ApplyOnHandChange(25m);
            await db.InventoryBalances.AddAsync(bal);
            await db.SaveChangesAsync();
        }

        // 6. Attempt deactivation of bin -> blocked by on-hand stock
        var deactBinRes = await client.DeleteAsync($"/api/warehouses/locations/{binId}");
        Assert.Equal(HttpStatusCode.Conflict, deactBinRes.StatusCode);
        var binErr = await deactBinRes.Content.ReadAsStringAsync();
        Assert.Contains("on-hand stock", binErr, StringComparison.OrdinalIgnoreCase);

        // 7. Attempt deactivation of area -> blocked by active child AND stock
        var deactAreaRes = await client.DeleteAsync($"/api/warehouses/locations/{areaId}");
        Assert.Equal(HttpStatusCode.Conflict, deactAreaRes.StatusCode);

        // 8. Attempt deactivation of zone -> blocked by active locations and stock
        var deactZoneRes = await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.Conflict, deactZoneRes.StatusCode);

        // 9. Attempt deactivation of warehouse -> blocked by active locations and stock
        var deactWhRes = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.Conflict, deactWhRes.StatusCode);

        // 10. Clear on-hand stock in DB
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var bal = await db.InventoryBalances.FirstAsync(b => b.InventoryBalanceId == balanceId);
            bal.ApplyOnHandChange(-25m);
            await db.SaveChangesAsync();
        }

        // 11. Now deactivations should succeed bottom-up
        var cleanDeactBin = await client.DeleteAsync($"/api/warehouses/locations/{binId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactBin.StatusCode);

        var cleanDeactArea = await client.DeleteAsync($"/api/warehouses/locations/{areaId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactArea.StatusCode);

        var cleanDeactZone = await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactZone.StatusCode);

        var cleanDeactWh = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactWh.StatusCode);

        // 12. Verify DB state: historical records are preserved with IsActive = false
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var whDb = await db.Warehouses.FindAsync(whId);
            Assert.NotNull(whDb);
            Assert.False(whDb.IsActive);

            var zoneDb = await db.WarehouseZones.FindAsync(zoneId);
            Assert.NotNull(zoneDb);
            Assert.False(zoneDb.IsActive);

            var areaDb = await db.WarehouseLocations.FindAsync(areaId);
            Assert.NotNull(areaDb);
            Assert.False(areaDb.IsActive);

            var binDb = await db.WarehouseLocations.FindAsync(binId);
            Assert.NotNull(binDb);
            Assert.False(binDb.IsActive);

            var balDb = await db.InventoryBalances.FindAsync(balanceId);
            Assert.NotNull(balDb);
        }
    }

    [Fact]
    public async Task Deactivation_Blocked_WhenPendingDraftDocumentsExist_AndPermittedAfterResolution()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.DEACTIVATE", "WAREHOUSE.ACTIVATE", "WAREHOUSE.MANAGE_ZONES",
                "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.DELETE", "LOCATION.DEACTIVATE", "LOCATION.ACTIVATE"
            });

        // 1. Create warehouse and two locations
        string whCode = $"WH_{Guid.NewGuid():N}"[..8];
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            City = "Colombo"
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        string locCodeA = $"LOCA_{Guid.NewGuid():N}"[..8];
        var locARes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = locCodeA,
            LocationName = $"Location {locCodeA}"
        });
        var locA = await locARes.ReadDataJsonElementAsync();
        var locAId = locA.GetProperty("warehouseLocationId").GetGuid();

        string locCodeB = $"LOCB_{Guid.NewGuid():N}"[..8];
        var locBRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = locCodeB,
            LocationName = $"Location {locCodeB}"
        });
        var locB = await locBRes.ReadDataJsonElementAsync();
        var locBId = locB.GetProperty("warehouseLocationId").GetGuid();

        // 2. Setup product in DB
        Guid prodId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var prod = Product.Create(
                tenantId: TestConstants.TenantAlphaId,
                productCode: $"PRD_{Guid.NewGuid():N}"[..8],
                productName: $"Draft Doc Product {Guid.NewGuid():N}"[..20],
                categoryId: TestConstants.CategoryAlphaId,
                unitOfMeasure: "PCS",
                description: null,
                isStockTracked: true,
                productType: ProductType.StockItem);
            await db.Products.AddAsync(prod);
            await db.SaveChangesAsync();
            prodId = prod.ProductId;
        }

        // 3. Test Draft Goods Receipt Note blocking
        Guid grnId = Guid.NewGuid();
        string grnNum = $"GRN-{Guid.NewGuid():N}"[..10];
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var po = PurchaseOrder.Create(
                TestConstants.TenantAlphaId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                $"PO-{Guid.NewGuid():N}"[..10]);
            var poItem = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = prodId,
                Quantity = 10m,
                UnitPrice = 50m,
                LineTotal = 500m,
                SubTotal = 500m,
                IsActive = true
            };
            po.Items.Add(poItem);
            await db.PurchaseOrders.AddAsync(po);
            await db.SaveChangesAsync();

            var grn = GoodsReceiptNote.Create(
                TestConstants.TenantAlphaId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                po.Id,
                grnNum);
            grn.GoodsReceiptNoteId = grnId;
            var line = GoodsReceiptNoteLine.Create(
                grnId,
                poItem.Id,
                prodId,
                whId,
                locAId,
                10m,
                50m);
            grn.Lines.Add(line);
            await db.GoodsReceiptNotes.AddAsync(grn);
            await db.SaveChangesAsync();
        }

        var deactLocBlockedByGrn = await client.DeleteAsync($"/api/warehouses/locations/{locAId}");
        Assert.Equal(HttpStatusCode.Conflict, deactLocBlockedByGrn.StatusCode);
        var grnLocErr = await deactLocBlockedByGrn.Content.ReadAsStringAsync();
        Assert.Contains("Goods Receipt Note", grnLocErr, StringComparison.OrdinalIgnoreCase);

        var deactWhBlockedByGrn = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.Conflict, deactWhBlockedByGrn.StatusCode);

        // Cancel the draft GRN
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var grn = await db.GoodsReceiptNotes.FirstAsync(g => g.GoodsReceiptNoteId == grnId);
            grn.Status = GoodsReceiptNoteStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        // 4. Test Draft Putaway blocking (source on locAId, destination on locBId)
        Guid putawayId;
        string putawayNum = $"PUT-{Guid.NewGuid():N}"[..10];
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var putaway = Putaway.Create(
                TestConstants.TenantAlphaId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                putawayNum,
                PutawaySourceDocumentType.GoodsReceipt,
                grnId,
                grnNum,
                whId);
            var pLine = PutawayLine.Create(
                putaway.PutawayId,
                Guid.NewGuid(),
                prodId,
                locAId,
                10m,
                0m,
                10m,
                10m);
            var alloc = PutawayLineAllocation.Create(
                pLine.PutawayLineId,
                locBId,
                10m);
            pLine.Allocations.Add(alloc);
            putaway.Lines.Add(pLine);
            await db.Putaways.AddAsync(putaway);
            await db.SaveChangesAsync();
            putawayId = putaway.PutawayId;
        }

        var deactLocBlockedByPutawaySrc = await client.DeleteAsync($"/api/warehouses/locations/{locAId}");
        Assert.Equal(HttpStatusCode.Conflict, deactLocBlockedByPutawaySrc.StatusCode);
        var putSrcErr = await deactLocBlockedByPutawaySrc.Content.ReadAsStringAsync();
        Assert.Contains("Putaway", putSrcErr, StringComparison.OrdinalIgnoreCase);

        var deactLocBlockedByPutawayDest = await client.DeleteAsync($"/api/warehouses/locations/{locBId}");
        Assert.Equal(HttpStatusCode.Conflict, deactLocBlockedByPutawayDest.StatusCode);
        var putDestErr = await deactLocBlockedByPutawayDest.Content.ReadAsStringAsync();
        Assert.Contains("Putaway", putDestErr, StringComparison.OrdinalIgnoreCase);

        // Cancel the draft Putaway
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var putaway = await db.Putaways.FirstAsync(p => p.PutawayId == putawayId);
            putaway.Status = PutawayStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        // 5. Test Draft Sales Shipment blocking
        Guid shipmentId;
        string shipmentNum = $"SHP-{Guid.NewGuid():N}"[..10];
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var so = SalesOrder.Create(
                TestConstants.TenantAlphaId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                TestConstants.CustomerAlphaId,
                $"SO-SHP-{Guid.NewGuid():N}"[..12]);
            var soItem = new SalesOrderItem
            {
                Id = Guid.NewGuid(),
                SalesOrderId = so.Id,
                ProductId = prodId,
                Quantity = 5m,
                UnitPrice = 100m,
                LineTotal = 500m,
                SubTotal = 500m,
                IsActive = true
            };
            so.Items.Add(soItem);
            await db.SalesOrders.AddAsync(so);
            await db.SaveChangesAsync();

            var shipment = SalesShipment.Create(
                TestConstants.TenantAlphaId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                so.Id,
                TestConstants.CustomerAlphaId,
                shipmentNum,
                FulfillmentMethod.Delivery,
                whId);
            var sItem = SalesShipmentItem.Create(
                shipment.Id,
                soItem.Id,
                prodId,
                whId,
                locAId,
                5m);
            shipment.Items.Add(sItem);
            await db.SalesShipments.AddAsync(shipment);
            await db.SaveChangesAsync();
            shipmentId = shipment.Id;
        }

        var deactLocBlockedByShipment = await client.DeleteAsync($"/api/warehouses/locations/{locAId}");
        Assert.Equal(HttpStatusCode.Conflict, deactLocBlockedByShipment.StatusCode);
        var shpErr = await deactLocBlockedByShipment.Content.ReadAsStringAsync();
        Assert.Contains("Sales Shipment", shpErr, StringComparison.OrdinalIgnoreCase);

        // Cancel the draft Shipment
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var shipment = await db.SalesShipments.FirstAsync(s => s.Id == shipmentId);
            shipment.Status = SalesShipmentStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        // 6. After resolving all pending draft operations, deactivations succeed
        var cleanDeactA = await client.DeleteAsync($"/api/warehouses/locations/{locAId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactA.StatusCode);

        var cleanDeactB = await client.DeleteAsync($"/api/warehouses/locations/{locBId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactB.StatusCode);

        var cleanDeactWh = await client.DeleteAsync($"/api/warehouses/{whId}");
        Assert.Equal(HttpStatusCode.NoContent, cleanDeactWh.StatusCode);
    }

    [Fact]
    public async Task Reactivation_AndHierarchySafety_EnforcesPrerequisites()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "WAREHOUSE.DEACTIVATE", "WAREHOUSE.ACTIVATE", "WAREHOUSE.MANAGE_ZONES",
                "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.DELETE", "LOCATION.DEACTIVATE", "LOCATION.ACTIVATE"
            });

        // 1. Create warehouse, zone, location
        string whCode = $"WH_{Guid.NewGuid():N}"[..8];
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            City = "Colombo"
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        string zoneCode = $"ZN_{Guid.NewGuid():N}"[..8];
        var zoneRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = zoneCode,
            ZoneName = $"Zone {zoneCode}"
        });
        var zone = await zoneRes.ReadDataJsonElementAsync();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        string locCode = $"LOC_{Guid.NewGuid():N}"[..8];
        var locRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = locCode,
            LocationName = $"Location {locCode}",
            WarehouseZoneId = zoneId
        });
        var loc = await locRes.ReadDataJsonElementAsync();
        var locId = loc.GetProperty("warehouseLocationId").GetGuid();

        // 2. Deactivate location, zone, warehouse cleanly
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/locations/{locId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/{whId}")).StatusCode);

        // 3. Attempt to activate zone while warehouse is inactive -> 409 Conflict
        var actZoneWhInactiveRes = await client.PostAsync($"/api/warehouses/{whId}/zones/{zoneId}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, actZoneWhInactiveRes.StatusCode);
        var actZoneErr = await actZoneWhInactiveRes.Content.ReadAsStringAsync();
        Assert.Contains("owning warehouse is inactive", actZoneErr, StringComparison.OrdinalIgnoreCase);

        // 4. Attempt to activate location while warehouse is inactive -> 409 Conflict
        var actLocWhInactiveRes = await client.PostAsync($"/api/warehouses/locations/{locId}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, actLocWhInactiveRes.StatusCode);
        var actLocErr = await actLocWhInactiveRes.Content.ReadAsStringAsync();
        Assert.Contains("owning warehouse is inactive", actLocErr, StringComparison.OrdinalIgnoreCase);

        // 5. Activate warehouse -> 200 OK
        var actWhRes = await client.PostAsync($"/api/warehouses/{whId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, actWhRes.StatusCode);
        var actWhData = await actWhRes.ReadDataJsonElementAsync();
        Assert.True(actWhData.GetProperty("isActive").GetBoolean());

        // 6. Repeated warehouse activation is idempotent -> 200 OK
        var actWhIdempotentRes = await client.PostAsync($"/api/warehouses/{whId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, actWhIdempotentRes.StatusCode);

        // 7. Attempt to activate location while zone is still inactive -> 409 Conflict
        var actLocZoneInactiveRes = await client.PostAsync($"/api/warehouses/locations/{locId}/activate", null);
        Assert.Equal(HttpStatusCode.Conflict, actLocZoneInactiveRes.StatusCode);
        var actLocZoneErr = await actLocZoneInactiveRes.Content.ReadAsStringAsync();
        Assert.Contains("assigned zone is inactive", actLocZoneErr, StringComparison.OrdinalIgnoreCase);

        // 8. Activate zone -> 200 OK
        var actZoneRes = await client.PostAsync($"/api/warehouses/{whId}/zones/{zoneId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, actZoneRes.StatusCode);
        var actZoneData = await actZoneRes.ReadDataJsonElementAsync();
        Assert.True(actZoneData.GetProperty("isActive").GetBoolean());

        // 9. Activate location -> 200 OK
        var actLocRes = await client.PostAsync($"/api/warehouses/locations/{locId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, actLocRes.StatusCode);
        var actLocData = await actLocRes.ReadDataJsonElementAsync();
        Assert.True(actLocData.GetProperty("isActive").GetBoolean());

        // 10. Deactivate again to test creation blockers under inactive warehouse
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/locations/{locId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/{whId}/zones/{zoneId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/{whId}")).StatusCode);

        var createZoneUnderInactiveRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = $"ZN_NEW_{Guid.NewGuid():N}"[..8],
            ZoneName = "New Zone"
        });
        Assert.Equal(HttpStatusCode.Conflict, createZoneUnderInactiveRes.StatusCode);

        var createLocUnderInactiveRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = $"LOC_NEW_{Guid.NewGuid():N}"[..8],
            LocationName = "New Location"
        });
        Assert.Equal(HttpStatusCode.Conflict, createLocUnderInactiveRes.StatusCode);
    }

    [Fact]
    public async Task Dispatch_LocationPickingCapability_EnforcesCanPick()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User", "Tenant Admin" },
            new[]
            {
                "SALES_ORDER.VIEW", "SALES_ORDER.CREATE", "SALES_ORDER.EDIT", "SALES_ORDER.SUBMIT", "SALES_ORDER.CONFIRM",
                "SALES_SHIPMENT.VIEW", "SALES_SHIPMENT.CREATE", "SALES_SHIPMENT.EDIT", "SALES_SHIPMENT.DISPATCH", "SALES_SHIPMENT.DELIVER", "SALES_SHIPMENT.CANCEL",
                "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "LOCATION.VIEW", "LOCATION.CREATE"
            });

        // 1. Create warehouse scoped to CompanyA and BranchA1
        string whCode = $"WH_{Guid.NewGuid():N}"[..8];
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            City = "Colombo",
            CompanyId = TestConstants.CompanyAId,
            BranchId = TestConstants.BranchA1Id
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create Location Types: One with CanPick=false, one with CanPick=true
        var noPickTypeRes = await client.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"NOPK_{Guid.NewGuid():N}"[..8],
            Name = "Quarantine / Staging (No Pick)",
            CanStoreInventory = true,
            CanPick = false,
            CanPutAway = true,
            CanContainChildren = false
        });
        var noPickType = await noPickTypeRes.ReadDataJsonElementAsync();
        var noPickTypeId = noPickType.GetProperty("locationTypeId").GetGuid();

        var pickTypeRes = await client.PostAsJsonAsync("/api/warehouses/location-types", new CreateLocationTypeRequest
        {
            Code = $"PICK_{Guid.NewGuid():N}"[..8],
            Name = "Pick Bin",
            CanStoreInventory = true,
            CanPick = true,
            CanPutAway = true,
            CanContainChildren = false
        });
        var pickType = await pickTypeRes.ReadDataJsonElementAsync();
        var pickTypeId = pickType.GetProperty("locationTypeId").GetGuid();

        // 3. Create two locations
        var locNoPickRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = $"STG_{Guid.NewGuid():N}"[..8],
            LocationName = "Staging Area",
            LocationTypeId = noPickTypeId
        });
        var locNoPick = await locNoPickRes.ReadDataJsonElementAsync();
        var locNoPickId = locNoPick.GetProperty("warehouseLocationId").GetGuid();

        var locPickRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = $"BIN_{Guid.NewGuid():N}"[..8],
            LocationName = "Pick Bin 01",
            LocationTypeId = pickTypeId
        });
        var locPick = await locPickRes.ReadDataJsonElementAsync();
        var locPickId = locPick.GetProperty("warehouseLocationId").GetGuid();

        // 4. Create stock-tracked product and confirmed sales order
        Guid prodId;
        Guid orderId;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var prod = Product.Create(
                tenantId: TestConstants.TenantAlphaId,
                productCode: $"PRD_{Guid.NewGuid():N}"[..8],
                productName: $"Pickable Item {Guid.NewGuid():N}"[..20],
                categoryId: TestConstants.CategoryAlphaId,
                unitOfMeasure: "PCS",
                description: null,
                isStockTracked: true,
                productType: ProductType.StockItem);
            await db.Products.AddAsync(prod);
            await db.SaveChangesAsync();
            prodId = prod.ProductId;

            var order = SalesOrder.Create(
                tenantId: TestConstants.TenantAlphaId,
                companyId: TestConstants.CompanyAId,
                branchId: TestConstants.BranchA1Id,
                customerId: TestConstants.CustomerAlphaId,
                orderNumber: $"SO-{Guid.NewGuid():N}"[..12],
                documentType: SalesOrderType.Standard,
                orderDate: DateTime.UtcNow,
                currency: "LKR");
            var oItem = new SalesOrderItem
            {
                Id = Guid.NewGuid(),
                SalesOrderId = order.Id,
                ProductId = prodId,
                Quantity = 10m,
                UnitPrice = 100m,
                LineTotal = 1000m,
                SubTotal = 1000m,
                IsActive = true
            };
            order.Items.Add(oItem);
            order.Submit();
            order.Confirm();
            await db.SalesOrders.AddAsync(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
        }

        // 5. Deposit inventory into NO-PICK location
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var bal = InventoryBalance.Create(TestConstants.TenantAlphaId, whId, locNoPickId, prodId);
            bal.ApplyOnHandChange(20m);
            await db.InventoryBalances.AddAsync(bal);
            await db.SaveChangesAsync();
        }

        // 6. Query availability explicitly passing targetLocationId = locNoPickId -> rejected with 409 Conflict
        var availRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{orderId}/availability?targetLocationId={locNoPickId}");
        Assert.Equal(HttpStatusCode.Conflict, availRes.StatusCode);
        var availErr = await availRes.Content.ReadAsStringAsync();
        Assert.Contains("does not allow picking", availErr, StringComparison.OrdinalIgnoreCase);

        // 7. Attempt shipment creation explicitly targeting locNoPickId -> rejected with 409 Conflict
        var fulfillNoPickRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{orderId}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"SHP-NOPK-{Guid.NewGuid():N}"[..12],
                DefaultWarehouseId = whId,
                DefaultLocationId = locNoPickId
            });
        Assert.Equal(HttpStatusCode.Conflict, fulfillNoPickRes.StatusCode);
        var quickErr = await fulfillNoPickRes.Content.ReadAsStringAsync();
        Assert.Contains("does not allow picking", quickErr, StringComparison.OrdinalIgnoreCase);

        // 8. Deposit inventory into pickable location
        using (var scope = _fixture.Services.CreateScope())
        {
            var postingService = scope.ServiceProvider.GetRequiredService<IInventoryPostingService>();
            await postingService.PostMovementAsync(TestConstants.TenantAlphaId, new PostInventoryMovementRequest
            {
                MovementType = InventoryMovementType.StockAdjustmentIn,
                SourceDocumentType = SourceDocumentType.StockAdjustment,
                SourceDocumentId = Guid.NewGuid(),
                SourceDocumentLineId = Guid.NewGuid(),
                MovementSequence = 1,
                ProductId = prodId,
                WarehouseId = whId,
                WarehouseLocationId = locPickId,
                Quantity = 20m,
                UnitCost = 10m,
                TransactionDate = DateTime.UtcNow
            });
        }

        // 9. Shipment creation targeting pickable location -> succeeds!
        var fulfillPickRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/from-order/{orderId}",
            new CreateShipmentFromOrderRequest
            {
                ShipmentNumber = $"SHP-PICK-{Guid.NewGuid():N}"[..12],
                DefaultWarehouseId = whId,
                DefaultLocationId = locPickId
            });
        Assert.Equal(HttpStatusCode.Created, fulfillPickRes.StatusCode);
        var shipmentData = await fulfillPickRes.ReadDataJsonElementAsync();
        var shipmentId = shipmentData.GetProperty("id").GetGuid();

        // 10. Dispatch shipment -> succeeds!
        var dispatchRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-shipments/{shipmentId}/dispatch",
            null);
        Assert.Equal(HttpStatusCode.OK, dispatchRes.StatusCode);
        var dispatchedData = await dispatchRes.ReadDataJsonElementAsync();
        Assert.Equal(2, dispatchedData.GetProperty("status").GetInt32()); // Dispatched
    }

    [Fact]
    public async Task Lifecycle_DedicatedPermissions_Enforced()
    {
        // 1. Setup clean warehouse and location using admin client
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.EDIT", "WAREHOUSE.DELETE", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.EDIT", "LOCATION.DELETE" });

        string whCode = $"WH_{Guid.NewGuid():N}"[..8];
        var whRes = await adminClient.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = $"Warehouse {whCode}",
            City = "Colombo"
        });
        var wh = await whRes.ReadDataJsonElementAsync();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        string locCode = $"LOC_{Guid.NewGuid():N}"[..8];
        var locRes = await adminClient.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = locCode,
            LocationName = $"Location {locCode}"
        });
        var loc = await locRes.ReadDataJsonElementAsync();
        var locId = loc.GetProperty("warehouseLocationId").GetGuid();

        // 2. Client with only WAREHOUSE.DEACTIVATE and LOCATION.DEACTIVATE (no ACTIVATE)
        var deactOnlyClient = _fixture.CreateAuthenticatedClient(
            Guid.NewGuid(),
            "deact_user",
            TestConstants.TenantAlphaId,
            new[] { "Deact Role" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.DEACTIVATE", "LOCATION.VIEW", "LOCATION.DEACTIVATE" });

        // Can deactivate location
        var locDeactRes = await deactOnlyClient.PostAsync($"/api/warehouses/locations/{locId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, locDeactRes.StatusCode);

        // Cannot activate location -> 403 Forbidden
        var locActForbidden = await deactOnlyClient.PostAsync($"/api/warehouses/locations/{locId}/activate", null);
        Assert.Equal(HttpStatusCode.Forbidden, locActForbidden.StatusCode);

        // Can deactivate warehouse
        var whDeactRes = await deactOnlyClient.PostAsync($"/api/warehouses/{whId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, whDeactRes.StatusCode);

        // Cannot activate warehouse -> 403 Forbidden
        var whActForbidden = await deactOnlyClient.PostAsync($"/api/warehouses/{whId}/activate", null);
        Assert.Equal(HttpStatusCode.Forbidden, whActForbidden.StatusCode);

        // 3. Client with only WAREHOUSE.ACTIVATE and LOCATION.ACTIVATE (no DEACTIVATE)
        var actOnlyClient = _fixture.CreateAuthenticatedClient(
            Guid.NewGuid(),
            "act_user",
            TestConstants.TenantAlphaId,
            new[] { "Act Role" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.ACTIVATE", "LOCATION.VIEW", "LOCATION.ACTIVATE" });

        // Can activate warehouse
        var whActRes = await actOnlyClient.PostAsync($"/api/warehouses/{whId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, whActRes.StatusCode);

        // Cannot deactivate warehouse -> 403 Forbidden
        var whDeactForbidden = await actOnlyClient.PostAsync($"/api/warehouses/{whId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, whDeactForbidden.StatusCode);

        // Can activate location
        var locActRes = await actOnlyClient.PostAsync($"/api/warehouses/locations/{locId}/activate", null);
        Assert.Equal(HttpStatusCode.OK, locActRes.StatusCode);

        // Cannot deactivate location -> 403 Forbidden
        var locDeactForbidden = await actOnlyClient.PostAsync($"/api/warehouses/locations/{locId}/deactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, locDeactForbidden.StatusCode);
    }
}

