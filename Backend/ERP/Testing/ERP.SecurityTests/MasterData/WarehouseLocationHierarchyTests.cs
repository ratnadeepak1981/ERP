using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ERP.Domain.Features.MasterData.Warehouse;
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
        var wh = await whRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var areaType = await areaTypeRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var binType = await binTypeRes.Content.ReadFromJsonAsync<JsonElement>();
        var binTypeId = binType.GetProperty("locationTypeId").GetGuid();

        // 3. Create unzoned root location (e.g., General Receiving Bay) directly under warehouse
        var unzonedLocRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BAY_UNZONED",
            LocationName = "Direct Warehouse Bay",
            LocationTypeId = areaTypeId
        });
        Assert.Equal(HttpStatusCode.OK, unzonedLocRes.StatusCode);
        var unzonedLoc = await unzonedLocRes.Content.ReadFromJsonAsync<JsonElement>();
        var unzonedLocId = unzonedLoc.GetProperty("warehouseLocationId").GetGuid();
        Assert.Null(unzonedLoc.GetProperty("warehouseZoneId").GetString());

        // 4. Create child under unzoned root location
        var childUnzonedRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "BAY_BIN_01",
            LocationName = "Bay Bin 01",
            LocationTypeId = binTypeId,
            ParentLocationId = unzonedLocId
        });
        Assert.Equal(HttpStatusCode.OK, childUnzonedRes.StatusCode);
        var childUnzoned = await childUnzonedRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(unzonedLocId, childUnzoned.GetProperty("parentLocationId").GetGuid());

        // 5. Create Zone in warehouse
        var zoneRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZONE_COLD",
            ZoneName = "Cold Storage Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var aisle = await aisleRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var rack = await rackRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var shelf = await shelfRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var bin = await binRes.Content.ReadFromJsonAsync<JsonElement>();
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
        var wh1 = await wh1Res.Content.ReadFromJsonAsync<JsonElement>();
        var wh1Id = wh1.GetProperty("warehouseId").GetGuid();

        // Warehouse 2
        var wh2Res = await clientA.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"WH2_{Guid.NewGuid():N}".Substring(0, 8),
            WarehouseName = $"Warehouse 2_{Guid.NewGuid():N}",
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, wh2Res.StatusCode);
        var wh2 = await wh2Res.Content.ReadFromJsonAsync<JsonElement>();
        var wh2Id = wh2.GetProperty("warehouseId").GetGuid();

        // Location in Warehouse 1
        var loc1Res = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "WH1_LOC_A",
            LocationName = "WH1 Location A"
        });
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<JsonElement>();
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
        var zoneA = await zoneARes.Content.ReadFromJsonAsync<JsonElement>();
        var zoneAId = zoneA.GetProperty("warehouseZoneId").GetGuid();

        var zoneBRes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZB",
            ZoneName = "Zone B"
        });
        var zoneB = await zoneBRes.Content.ReadFromJsonAsync<JsonElement>();
        var zoneBId = zoneB.GetProperty("warehouseZoneId").GetGuid();

        // Location in Zone A
        var locZoneARes = await clientA.PostAsJsonAsync($"/api/warehouses/{wh1Id}/zones/{zoneAId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "ZA_LOC_1",
            LocationName = "Zone A Location 1"
        });
        var locZoneA = await locZoneARes.Content.ReadFromJsonAsync<JsonElement>();
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
            WarehouseCode = $"WH_T1_{Guid.NewGuid():N}".Substring(0, 8),
            WarehouseName = $"Tenant 1 Warehouse_{Guid.NewGuid():N}",
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.Content.ReadFromJsonAsync<JsonElement>();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        var locRes = await clientA.PostAsJsonAsync($"/api/warehouses/{whId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "LOC_SECRET",
            LocationName = "Secret Location"
        });
        Assert.Equal(HttpStatusCode.OK, locRes.StatusCode);
        var loc = await locRes.Content.ReadFromJsonAsync<JsonElement>();
        var locId = loc.GetProperty("warehouseLocationId").GetGuid();

        // Tenant B attempts to get Tenant A warehouse locations -> Returns empty list
        var tenantBGetRes = await clientB.GetAsync($"/api/warehouses/{whId}/locations");
        Assert.Equal(HttpStatusCode.OK, tenantBGetRes.StatusCode);
        var tenantBLocs = await tenantBGetRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.Empty(tenantBLocs!);

        // Tenant B attempts to create child location under Tenant A location in their own warehouse -> Rejected
        var whBRes = await clientB.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = $"WH_T2_{Guid.NewGuid():N}".Substring(0, 8),
            WarehouseName = $"Tenant 2 Warehouse_{Guid.NewGuid():N}",
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, whBRes.StatusCode);
        var whB = await whBRes.Content.ReadFromJsonAsync<JsonElement>();
        var whBId = whB.GetProperty("warehouseId").GetGuid();

        var crossTenantParentRes = await clientB.PostAsJsonAsync($"/api/warehouses/{whBId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "TB_ATTEMPT",
            LocationName = "Tenant B Attempt",
            ParentLocationId = locId
        });
        Assert.Equal(HttpStatusCode.Conflict, crossTenantParentRes.StatusCode);
    }
}
