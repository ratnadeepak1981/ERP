using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.UnitOfMeasure;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class UnitOfMeasureConversionTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public UnitOfMeasureConversionTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task DirectAndInverseConversion_CalculatesAccurately()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        // Create BOX unit
        var boxRes = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"BOX_{uid}",
            Name = "Box Unit"
        });
        var boxId = (await boxRes.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Create PCS unit
        var pcsRes = await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"PCS_{uid}",
            Name = "Piece Unit"
        });
        var pcsId = (await pcsRes.ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Create conversion: 1 BOX = 12 PCS
        var convRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = boxId,
            ToUnitId = pcsId,
            ConversionFactor = 12m
        });
        Assert.Equal(HttpStatusCode.OK, convRes.StatusCode);

        // Direct: 5 BOX -> PCS
        var directRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={boxId}&toUnitId={pcsId}&quantity=5");
        Assert.Equal(HttpStatusCode.OK, directRes.StatusCode);
        var directData = await directRes.ReadDataJsonElementAsync();
        Assert.Equal(60m, directData.GetProperty("convertedQuantity").GetDecimal());
        Assert.Equal(12m, directData.GetProperty("effectiveFactor").GetDecimal());
        Assert.False(directData.GetProperty("isInverse").GetBoolean());

        // Inverse: 60 PCS -> BOX
        var inverseRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={pcsId}&toUnitId={boxId}&quantity=60");
        Assert.Equal(HttpStatusCode.OK, inverseRes.StatusCode);
        var inverseData = await inverseRes.ReadDataJsonElementAsync();
        Assert.Equal(5m, inverseData.GetProperty("convertedQuantity").GetDecimal());
        Assert.True(inverseData.GetProperty("isInverse").GetBoolean());

        // Identity: 10 BOX -> BOX
        var identityRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={boxId}&toUnitId={boxId}&quantity=10");
        Assert.Equal(HttpStatusCode.OK, identityRes.StatusCode);
        var identityData = await identityRes.ReadDataJsonElementAsync();
        Assert.Equal(10m, identityData.GetProperty("convertedQuantity").GetDecimal());
        Assert.Equal(1m, identityData.GetProperty("effectiveFactor").GetDecimal());
    }

    [Fact]
    public async Task MissingConversion_ReturnsConflict()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        var uom1 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"KG_{uid}",
            Name = "Kilogram"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var uom2 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"M_{uid}",
            Name = "Meter"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // No conversion defined between KG and Meter -> returns Conflict
        var res = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={uom1}&toUnitId={uom2}&quantity=10");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task CrossTenantConversionQuery_IsRejected()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        // Tenant A unit
        var uomA = (await (await clientA.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"TA_{uid}",
            Name = "Tenant A Unit"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Tenant B unit
        var uomB = (await (await clientB.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"TB_{uid}",
            Name = "Tenant B Unit"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Tenant A tries to query convert between its unit and Tenant B's unit
        var convertRes = await clientA.GetAsync($"/api/uom/convert?fromUnitId={uomA}&toUnitId={uomB}&quantity=5");
        Assert.Equal(HttpStatusCode.Conflict, convertRes.StatusCode);
    }
}
