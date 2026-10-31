using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.TaxCode;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class TaxCodeTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public TaxCodeTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateTaxCode_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "TAX_CODE.VIEW", "TAX_CODE.CREATE" });

        var code = $"VAT{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var name = $"VAT Standard {code}";
        var response = await client.PostAsJsonAsync("/api/tax-codes", new CreateTaxCodeRequest
        {
            Code = code,
            Name = name,
            Rate = 15.00m,
            Applicability = TaxApplicability.Both,
            Description = "Standard 15% VAT"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, created.GetProperty("code").GetString());
        Assert.Equal(name, created.GetProperty("name").GetString());
        Assert.Equal(15.00m, created.GetProperty("rate").GetDecimal());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateAndReactivateTaxCode_UpdatesStatusCorrectly()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "TAX_CODE.VIEW", "TAX_CODE.CREATE", "TAX_CODE.EDIT", "TAX_CODE.DELETE" });

        var code = $"TAX{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/tax-codes", new CreateTaxCodeRequest
        {
            Code = code,
            Name = $"Tax {code}",
            Rate = 10.00m,
            Applicability = TaxApplicability.Sales
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.ReadDataJsonElementAsync();
        var id = created.GetProperty("taxCodeId").GetGuid();

        // Deactivate
        var deleteRes = await client.DeleteAsync($"/api/tax-codes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Reactivate
        var reactivateRes = await client.PostAsync($"/api/tax-codes/{id}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);
        var reactivated = await reactivateRes.ReadDataJsonElementAsync();
        Assert.True(reactivated.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task TaxCode_TenantIsolation_TenantBetaCannotSeeTenantAlphaTaxCode()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "TAX_CODE.VIEW", "TAX_CODE.CREATE" });

        var codeA = $"A_{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createRes = await clientA.PostAsJsonAsync("/api/tax-codes", new CreateTaxCodeRequest
        {
            Code = codeA,
            Name = $"Alpha Tax {codeA}",
            Rate = 7.5m
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.ReadDataJsonElementAsync();
        var idA = created.GetProperty("taxCodeId").GetGuid();

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "TAX_CODE.VIEW", "TAX_CODE.CREATE" });

        var getResB = await clientB.GetAsync($"/api/tax-codes/{idA}");
        Assert.Equal(HttpStatusCode.NotFound, getResB.StatusCode);
    }
}
