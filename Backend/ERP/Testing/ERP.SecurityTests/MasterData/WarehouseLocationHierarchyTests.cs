using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ERP.Domain.Features.MasterData.Warehouse;
using ERP.SecurityTests;
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
}

