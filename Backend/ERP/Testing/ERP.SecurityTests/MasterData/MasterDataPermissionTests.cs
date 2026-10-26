using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Product;
using Domain.Features.MasterData.UnitOfMeasure;
using ERP.Domain.Features.MasterData.Category;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class MasterDataPermissionTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public MasterDataPermissionTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task UpdateCategory_WithCategoryEditPermission_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CATEGORY.VIEW", "CATEGORY.EDIT" });

        var response = await client.PutAsJsonAsync($"/api/categories/{TestConstants.CategoryAlphaId}", new UpdateCategoryRequest
        {
            CategoryName = "Updated Category Alpha",
            Description = "Updated Description"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithoutCategoryEditPermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "CATEGORY.VIEW" });

        var response = await client.PutAsJsonAsync($"/api/categories/{TestConstants.CategoryAlphaId}", new UpdateCategoryRequest
        {
            CategoryName = "Unauthorized Attempt",
            Description = "Should Fail"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUnitOfMeasure_WithUomEditPermission_ReturnsOk()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.EDIT" });

        var uomRes = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"UOM_{Guid.NewGuid():N}"[..8],
            Name = "Initial Uom Name"
        });
        var uomData = await uomRes.ReadDataJsonElementAsync();
        var uomId = uomData.GetProperty("unitOfMeasureId").GetGuid();

        var editClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "UOM.VIEW", "UOM.EDIT" });

        var response = await editClient.PutAsJsonAsync($"/api/uom/{uomId}", new UpdateUnitOfMeasureRequest
        {
            Name = "Updated Uom Name",
            Description = "Updated Description"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUnitOfMeasure_WithoutUomEditPermission_ReturnsForbidden()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        var uomRes = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"UOM_{Guid.NewGuid():N}"[..8],
            Name = "Test Uom"
        });
        var uomData = await uomRes.ReadDataJsonElementAsync();
        var uomId = uomData.GetProperty("unitOfMeasureId").GetGuid();

        var noEditClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "UOM.VIEW" });

        var response = await noEditClient.PutAsJsonAsync($"/api/uom/{uomId}", new UpdateUnitOfMeasureRequest
        {
            Name = "Should Fail",
            Description = "Should Fail"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateConversion_WithUomConvertPermission_ReturnsSuccess()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var uom1Res = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U1_{Guid.NewGuid():N}"[..8],
            Name = "Unit 1"
        });
        var id1 = (await uom1Res.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var uom2Res = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U2_{Guid.NewGuid():N}"[..8],
            Name = "Unit 2"
        });
        var id2 = (await uom2Res.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var convRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = id1,
            ToUnitId = id2,
            ConversionFactor = 12m
        });

        Assert.Equal(HttpStatusCode.OK, convRes.StatusCode);
    }

    [Fact]
    public async Task CreateConversion_WithoutUomConvertPermission_ReturnsForbidden()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        var uom1Res = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U3_{Guid.NewGuid():N}"[..8],
            Name = "Unit 3"
        });
        var id1 = (await uom1Res.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var uom2Res = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U4_{Guid.NewGuid():N}"[..8],
            Name = "Unit 4"
        });
        var id2 = (await uom2Res.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var clientWithoutConvert = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "UOM.VIEW", "UOM.EDIT" }); // Has EDIT but not CONVERT

        var response = await clientWithoutConvert.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = id1,
            ToUnitId = id2,
            ConversionFactor = 10m
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplier_WithSupplierEditPermission_ReturnsOk()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "SUPPLIER.VIEW", "SUPPLIER.CREATE", "SUPPLIER.EDIT" });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var supRes = await adminClient.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"SUP_{uniqueId}",
            SupplierName = $"Initial Supplier {uniqueId}",
            City = "Cityville"
        });
        var supId = (await supRes.ReadDataJsonElementAsync()).GetProperty("supplierId").GetGuid();

        var editClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "SUPPLIER.VIEW", "SUPPLIER.EDIT" });

        var response = await editClient.PutAsJsonAsync($"/api/suppliers/{supId}", new UpdateSupplierRequest
        {
            SupplierName = $"Updated Supplier {uniqueId}",
            City = "Updated City",
            PaymentTerms = "Net 30"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplier_WithoutSupplierEditPermission_ReturnsForbidden()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "SUPPLIER.VIEW", "SUPPLIER.CREATE" });

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var supRes = await adminClient.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"SUP_{uniqueId}",
            SupplierName = $"Supplier Fail {uniqueId}",
            City = "Cityville"
        });
        var supId = (await supRes.ReadDataJsonElementAsync()).GetProperty("supplierId").GetGuid();

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "SUPPLIER.VIEW" });

        var response = await client.PutAsJsonAsync($"/api/suppliers/{supId}", new UpdateSupplierRequest
        {
            SupplierName = "Unauthorized Supplier",
            City = "Fail City"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithProductEditPermission_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW", "PRODUCT.EDIT" });

        var response = await client.PutAsJsonAsync($"/api/products/{TestConstants.ProductAlphaId}", new UpdateProductRequest
        {
            ProductName = "Updated Product Alpha",
            CategoryId = TestConstants.CategoryAlphaId,
            Description = "Updated Desc"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithoutProductEditPermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "PRODUCT.VIEW" });

        var response = await client.PutAsJsonAsync($"/api/products/{TestConstants.ProductAlphaId}", new UpdateProductRequest
        {
            ProductName = "Fail Product Alpha",
            Description = "Should Fail"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
