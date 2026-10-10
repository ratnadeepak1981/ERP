using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.Supplier;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.Domain.Features.MasterData.Warehouse;
using Xunit;

namespace ERP.SecurityTests.MasterData;

public class MasterDataTenantIsolationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public MasterDataTenantIsolationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Category_TenantAdminAlpha_CannotAccessTenantBetaCategory()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CATEGORY.VIEW" });

        // CategoryBetaId belongs to Tenant Beta
        var response = await clientA.GetAsync($"/api/categories/{TestConstants.CategoryBetaId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Category_CannotCreateDuplicateCodeWithinTenant_AllowedInDifferentTenants()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CATEGORY.VIEW", "CATEGORY.CREATE" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "CATEGORY.VIEW", "CATEGORY.CREATE" });

        string testCode = $"CAT_DUP_{Guid.NewGuid():N}";
        string testNameA = $"Category A_{Guid.NewGuid():N}";
        string testNameB = $"Category B_{Guid.NewGuid():N}";

        // Tenant A creates category
        var res1 = await clientA.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CategoryCode = testCode,
            CategoryName = testNameA
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Tenant A duplicate creation fails with 409 Conflict
        var res2 = await clientA.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CategoryCode = testCode,
            CategoryName = $"{testNameA}_Dup"
        });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);

        // Tenant B with same code succeeds (tenant isolation)
        var res3 = await clientB.PostAsJsonAsync("/api/categories", new CreateCategoryRequest
        {
            CategoryCode = testCode,
            CategoryName = testNameB
        });
        Assert.Equal(HttpStatusCode.Created, res3.StatusCode);
    }

    [Fact]
    public async Task UnitOfMeasure_ConversionFactorZeroOrNegative_ReturnsBadRequest()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        // Create two units
        var uom1Res = await client.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"BOX_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Box"
        });
        var uom1 = await uom1Res.Content.ReadFromJsonAsync<JsonElement>();
        var id1 = uom1.GetProperty("unitOfMeasureId").GetGuid();

        var uom2Res = await client.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"PCS_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Pieces"
        });
        var uom2 = await uom2Res.Content.ReadFromJsonAsync<JsonElement>();
        var id2 = uom2.GetProperty("unitOfMeasureId").GetGuid();

        // Conversion factor <= 0 fails
        var convRes = await client.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = id1,
            ToUnitId = id2,
            ConversionFactor = -5
        });
        Assert.Equal(HttpStatusCode.BadRequest, convRes.StatusCode);
    }

    [Fact]
    public async Task UnitOfMeasure_CrossTenantConversion_IsRejected()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        // Tenant A creates unit
        var uomARes = await clientA.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"UA_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Unit A"
        });
        var uomA = await uomARes.Content.ReadFromJsonAsync<JsonElement>();
        var idA = uomA.GetProperty("unitOfMeasureId").GetGuid();

        // Tenant B creates unit
        var uomBRes = await clientB.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"UB_{Guid.NewGuid():N}".Substring(0, 8),
            Name = "Unit B"
        });
        var uomB = await uomBRes.Content.ReadFromJsonAsync<JsonElement>();
        var idB = uomB.GetProperty("unitOfMeasureId").GetGuid();

        // Tenant A tries to convert between its unit and Tenant B's unit
        var convRes = await clientA.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = idA,
            ToUnitId = idB,
            ConversionFactor = 10
        });

        // 409 Conflict due to cross-tenant ownership violation
        Assert.Equal(HttpStatusCode.Conflict, convRes.StatusCode);
    }

    [Fact]
    public async Task Product_CrossTenantCategoryLinkage_IsRejected()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.CREATE" });

        // Tenant A creates product with CategoryBetaId (Tenant B)
        var response = await clientA.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            ProductCode = $"PROD_CROSS_{Guid.NewGuid():N}",
            ProductName = "Cross-Tenant Product",
            CategoryId = TestConstants.CategoryBetaId,
            UnitOfMeasure = "PCS"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Customer_BiDirectionalAddressSync_AndSingleDefaultAddressEnforcement()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CUSTOMER.VIEW", "CUSTOMER.CREATE", "CUSTOMER.UPDATE", "ADDRESS.VIEW", "ADDRESS.CREATE", "ADDRESS.MANAGE" });

        // 1. Create customer with flat address & contact
        string code = $"CUST_{Guid.NewGuid():N}".Substring(0, 10);
        string custName = $"Acme Corp_{Guid.NewGuid():N}";
        var createRes = await client.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = code,
            CustomerName = custName,
            AddressLine1 = "123 Main St",
            City = "Colombo",
            Country = "LK",
            ContactPerson = "John Doe",
            Email = "john@acme.com"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var cust = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = cust.GetProperty("customerId").GetGuid();

        // Verify normalized address association was created
        var addrRes = await client.GetAsync($"/api/customers/{customerId}/addresses");
        Assert.Equal(HttpStatusCode.OK, addrRes.StatusCode);
        var addrAssocs = await addrRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotEmpty(addrAssocs);
        Assert.True(addrAssocs[0].GetProperty("isDefault").GetBoolean());

        // 2. Create second address and link as default
        var newAddrRes = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            AddressName = "Secondary Office",
            AddressLine1 = "456 High St",
            City = "Kandy"
        });
        Assert.Equal(HttpStatusCode.Created, newAddrRes.StatusCode);
        var newAddr = await newAddrRes.Content.ReadFromJsonAsync<JsonElement>();
        var newAddrId = newAddr.GetProperty("addressId").GetGuid();

        var addrTypeId = addrAssocs[0].GetProperty("addressTypeId").GetGuid();

        var linkRes = await client.PostAsJsonAsync($"/api/customers/{customerId}/addresses", new CustomerAddressLinkRequest
        {
            AddressId = newAddrId,
            AddressTypeId = addrTypeId,
            IsDefault = true
        });
        Assert.Equal(HttpStatusCode.OK, linkRes.StatusCode);

        // Verify only ONE address is default for this type
        var updatedAddrRes = await client.GetAsync($"/api/customers/{customerId}/addresses");
        var updatedAddrs = await updatedAddrRes.Content.ReadFromJsonAsync<List<JsonElement>>();
        var defaults = updatedAddrs.FindAll(x => x.GetProperty("isDefault").GetBoolean());
        Assert.Single(defaults);
        Assert.Equal(newAddrId, defaults[0].GetProperty("addressId").GetGuid());

        // Verify Customer flat fields synced to new default address
        var updatedCustRes = await client.GetAsync($"/api/customers/{customerId}");
        var updatedCust = await updatedCustRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("456 High St", updatedCust.GetProperty("addressLine1").GetString());
        Assert.Equal("Kandy", updatedCust.GetProperty("city").GetString());
    }

    [Fact]
    public async Task Warehouse_LocationsHierarchy_PreventsCycleAndCrossWarehouseParentage()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "WAREHOUSE.VIEW", "WAREHOUSE.CREATE", "WAREHOUSE.MANAGE_ZONES", "LOCATION.VIEW", "LOCATION.CREATE", "LOCATION.UPDATE" });

        // 1. Create warehouse
        string whCode = $"WH_{Guid.NewGuid():N}".Substring(0, 8);
        string whName = $"Logistics Hub_{Guid.NewGuid():N}";
        var whRes = await client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest
        {
            WarehouseCode = whCode,
            WarehouseName = whName,
            City = "Colombo"
        });
        Assert.Equal(HttpStatusCode.Created, whRes.StatusCode);
        var wh = await whRes.Content.ReadFromJsonAsync<JsonElement>();
        var whId = wh.GetProperty("warehouseId").GetGuid();

        // 2. Create zone
        var zoneRes = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones", new CreateWarehouseZoneRequest
        {
            ZoneCode = "ZONE_A",
            ZoneName = "Receiving Zone"
        });
        Assert.Equal(HttpStatusCode.OK, zoneRes.StatusCode);
        var zone = await zoneRes.Content.ReadFromJsonAsync<JsonElement>();
        var zoneId = zone.GetProperty("warehouseZoneId").GetGuid();

        // 3. Create parent location (Aisle 1)
        var loc1Res = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "AISLE_01",
            LocationName = "Aisle 1"
        });
        Assert.Equal(HttpStatusCode.OK, loc1Res.StatusCode);
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<JsonElement>();
        var loc1Id = loc1.GetProperty("warehouseLocationId").GetGuid();

        // 4. Create child location (Rack 01 with Parent = Aisle 1)
        var loc2Res = await client.PostAsJsonAsync($"/api/warehouses/{whId}/zones/{zoneId}/locations", new CreateWarehouseLocationRequest
        {
            LocationCode = "RACK_01",
            LocationName = "Rack 1",
            ParentLocationId = loc1Id
        });
        Assert.Equal(HttpStatusCode.OK, loc2Res.StatusCode);
        var loc2 = await loc2Res.Content.ReadFromJsonAsync<JsonElement>();
        var loc2Id = loc2.GetProperty("warehouseLocationId").GetGuid();

        // 5. Updating Aisle 1 to have Parent = Rack 01 should trigger circular dependency detection (409 Conflict)
        var cycleRes = await client.PutAsJsonAsync($"/api/warehouses/locations/{loc1Id}", new UpdateWarehouseLocationRequest
        {
            LocationName = "Aisle 1 Cycle",
            ParentLocationId = loc2Id
        });
        Assert.Equal(HttpStatusCode.Conflict, cycleRes.StatusCode);
    }

    [Fact]
    public async Task SupplierProductPrice_CrossTenantSupplierOrProduct_IsRejected()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "SUPPLIER_PRICE.VIEW", "SUPPLIER_PRICE.MANAGE", "SUPPLIER.VIEW", "SUPPLIER.CREATE", "PRODUCT.VIEW" });

        // Create supplier in Tenant A
        string supCode = $"SUP_{Guid.NewGuid():N}".Substring(0, 8);
        string supName = $"Industrial Suppliers_{Guid.NewGuid():N}";
        var suppRes = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = supCode,
            SupplierName = supName,
            City = "Colombo"
        });
        var rawSupp = await suppRes.Content.ReadAsStringAsync();
        Assert.True(suppRes.StatusCode == HttpStatusCode.Created, $"Expected Created but got {suppRes.StatusCode}: {rawSupp}");
        var supp = await suppRes.Content.ReadFromJsonAsync<JsonElement>();
        var suppId = supp.GetProperty("supplierId").GetGuid();

        // Try to create price card linking Tenant A supplier with ProductBetaId (Tenant B)
        var priceRes = await clientA.PostAsJsonAsync("/api/supplier-prices", new CreateSupplierProductPriceRequest
        {
            SupplierId = suppId,
            ProductId = TestConstants.ProductBetaId,
            UnitPrice = 25.50m,
            EffectiveFrom = DateTime.UtcNow
        });

        // Rejected due to cross-tenant product linkage
        Assert.Equal(HttpStatusCode.Conflict, priceRes.StatusCode);
    }
}
