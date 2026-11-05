using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.UnitOfMeasure;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Domain.Infrastructure.Persistence;

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

    [Fact]
    public async Task ZeroAndNegativeFactor_IsRejectedWithBadRequest()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        var uom1 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U1_{uid}",
            Name = "Unit 1"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var uom2 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"U2_{uid}",
            Name = "Unit 2"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Zero factor
        var zeroRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = uom1,
            ToUnitId = uom2,
            ConversionFactor = 0m
        });
        Assert.Equal(HttpStatusCode.BadRequest, zeroRes.StatusCode);

        // Negative factor
        var negRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = uom1,
            ToUnitId = uom2,
            ConversionFactor = -2.5m
        });
        Assert.Equal(HttpStatusCode.BadRequest, negRes.StatusCode);
    }

    [Fact]
    public async Task SameSourceAndDestinationUom_ForConversionCreation_IsRejectedWithBadRequest()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        var uom1 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"IDN_{uid}",
            Name = "Identical Unit"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var res = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = uom1,
            ToUnitId = uom1,
            ConversionFactor = 1m
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task InactiveUom_InConversion_ReturnsConflict()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.DELETE", "UOM.CONVERT" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        var uom1 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"ACT_{uid}",
            Name = "Active Unit"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var uom2 = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"INA_{uid}",
            Name = "Inactive Unit"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // Create conversion
        var convRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = uom1,
            ToUnitId = uom2,
            ConversionFactor = 5m
        });
        Assert.Equal(HttpStatusCode.OK, convRes.StatusCode);

        // Deactivate uom2
        var delRes = await adminClient.DeleteAsync($"/api/uom/{uom2}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Convert query must fail with Conflict
        var convertRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={uom1}&toUnitId={uom2}&quantity=10");
        Assert.Equal(HttpStatusCode.Conflict, convertRes.StatusCode);
    }

    [Fact]
    public async Task PrecisionAndRounding_CalculatesAccurately()
    {
        var adminClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "UOM.VIEW", "UOM.CREATE", "UOM.CONVERT" });

        var uid = Guid.NewGuid().ToString("N")[..6];

        var kgId = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"KGP_{uid}",
            Name = "KG Precision"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        var lbId = (await (await adminClient.PostAsJsonAsync("/api/uom", new CreateUnitOfMeasureRequest
        {
            Code = $"LBP_{uid}",
            Name = "LB Precision"
        })).ReadDataJsonElementAsync()).GetProperty("unitOfMeasureId").GetGuid();

        // 1 KG = 2.20462 LB
        const decimal factor = 2.20462m;
        var convRes = await adminClient.PostAsJsonAsync("/api/uom/conversions", new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = kgId,
            ToUnitId = lbId,
            ConversionFactor = factor
        });
        Assert.Equal(HttpStatusCode.OK, convRes.StatusCode);

        // Direct: 10 KG -> LB = 22.0462
        var directRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={kgId}&toUnitId={lbId}&quantity=10");
        Assert.Equal(HttpStatusCode.OK, directRes.StatusCode);
        var directData = await directRes.ReadDataJsonElementAsync();
        Assert.Equal(22.0462m, directData.GetProperty("convertedQuantity").GetDecimal());
        Assert.Equal(factor, directData.GetProperty("effectiveFactor").GetDecimal());

        // Inverse: 22.0462 LB -> KG = 10
        var inverseRes = await adminClient.GetAsync($"/api/uom/convert?fromUnitId={lbId}&toUnitId={kgId}&quantity=22.0462");
        Assert.Equal(HttpStatusCode.OK, inverseRes.StatusCode);
        var inverseData = await inverseRes.ReadDataJsonElementAsync();
        Assert.Equal(10m, Math.Round(inverseData.GetProperty("convertedQuantity").GetDecimal(), 4));
        Assert.True(inverseData.GetProperty("isInverse").GetBoolean());
    }

    [Fact]
    public async Task CallerOwnedTransaction_ParticipatesWithoutCommitting()
    {
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DomainDbContext>();
        var uomService = sp.GetRequiredService<IUnitOfMeasureService>();

        var tenantId = TestConstants.TenantAlphaId;
        var uid = Guid.NewGuid().ToString("N")[..6];

        // Create UOMs
        var uom1 = await uomService.CreateUnitAsync(tenantId, new CreateUnitOfMeasureRequest { Code = $"TX1_{uid}", Name = "Tx Unit 1" });
        var uom2 = await uomService.CreateUnitAsync(tenantId, new CreateUnitOfMeasureRequest { Code = $"TX2_{uid}", Name = "Tx Unit 2" });
        await uomService.CreateConversionAsync(tenantId, new CreateUnitOfMeasureConversionRequest
        {
            FromUnitId = uom1.UnitOfMeasureId,
            ToUnitId = uom2.UnitOfMeasureId,
            ConversionFactor = 4m
        });

        // Open an explicit transaction
        await using var tx = await db.Database.BeginTransactionAsync();

        // Perform conversion query inside transaction
        var result = await uomService.ConvertQuantityAsync(tenantId, uom1.UnitOfMeasureId, uom2.UnitOfMeasureId, 5m);

        Assert.Equal(20m, result.ConvertedQuantity);
        Assert.Equal(4m, result.EffectiveFactor);
        Assert.False(result.IsInverse);

        // Roll back the transaction safely
        await tx.RollbackAsync();
    }
}
