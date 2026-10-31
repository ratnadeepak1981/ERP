using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Brand;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class BrandTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public BrandTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateBrand_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW", "BRAND.CREATE" });

        var code = $"BR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var name = $"Apex Brand {Guid.NewGuid():N}"[..20];
        var response = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = code,
            BrandName = name,
            Description = "Premium brand"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, created.GetProperty("brandCode").GetString());
        Assert.Equal(name, created.GetProperty("brandName").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateAndReactivateBrand_UpdatesStatusCorrectly()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW", "BRAND.CREATE", "BRAND.EDIT", "BRAND.DELETE" });

        var code = $"BR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = code,
            BrandName = $"Brand {code}"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var brandId = (await createRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetString();

        // Deactivate
        var deactRes = await client.DeleteAsync($"/api/brands/{brandId}");
        Assert.Equal(HttpStatusCode.NoContent, deactRes.StatusCode);

        var getDeact = await client.GetAsync($"/api/brands/{brandId}");
        var deactJson = await getDeact.ReadDataJsonElementAsync();
        Assert.False(deactJson.GetProperty("isActive").GetBoolean());

        // Reactivate
        var reactRes = await client.PostAsync($"/api/brands/{brandId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactRes.StatusCode);

        var getReact = await client.GetAsync($"/api/brands/{brandId}");
        var reactJson = await getReact.ReadDataJsonElementAsync();
        Assert.True(reactJson.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Brand_TenantIsolation_TenantBCannotAccessTenantABrand()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW", "BRAND.CREATE" });

        var code = $"BR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var createRes = await clientA.PostAsJsonAsync("/api/brands", new CreateBrandRequest
        {
            BrandCode = code,
            BrandName = $"Brand {code}"
        });
        var brandId = (await createRes.ReadDataJsonElementAsync()).GetProperty("brandId").GetString();

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "BRAND.VIEW" });

        var getRes = await clientB.GetAsync($"/api/brands/{brandId}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
