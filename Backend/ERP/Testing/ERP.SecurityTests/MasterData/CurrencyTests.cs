using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using Domain.Features.Procurement.PurchaseOrder;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class CurrencyTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public CurrencyTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateCurrency_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE" });

        var code = $"C{Guid.NewGuid():N}"[..3].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Test Dollar",
            Symbol = "$",
            DecimalPlaces = 2
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, created.GetProperty("currencyCode").GetString());
        Assert.Equal("Test Dollar", created.GetProperty("currencyName").GetString());
        Assert.Equal("$", created.GetProperty("symbol").GetString());
        Assert.Equal(2, created.GetProperty("decimalPlaces").GetInt32());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreateCurrency_DuplicateCodeInSameTenant_ReturnsConflict()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE" });

        string code;
        HttpResponseMessage res1;
        do
        {
            code = $"Z{Guid.NewGuid():N}"[..3].ToUpperInvariant();
            res1 = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
            {
                CurrencyCode = code,
                CurrencyName = "First Currency",
                Symbol = "F"
            });
        } while (res1.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Duplicate Currency",
            Symbol = "D"
        });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task CreateCurrency_SameCodeInDifferentTenants_BothSucceed()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE" });

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE" });

        var code = $"S{Guid.NewGuid():N}"[..3].ToUpperInvariant();

        var resA = await clientA.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Shared Code Tenant A",
            Symbol = "A"
        });
        Assert.Equal(HttpStatusCode.Created, resA.StatusCode);

        var resB = await clientB.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Shared Code Tenant B",
            Symbol = "B"
        });
        Assert.Equal(HttpStatusCode.Created, resB.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrency_WithValidData_ReturnsOk()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE", "CURRENCY.EDIT" });

        var code = $"U{Guid.NewGuid():N}"[..3].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Before Update",
            Symbol = "B",
            DecimalPlaces = 2
        });
        var currencyId = (await createRes.ReadDataJsonElementAsync()).GetProperty("currencyId").GetGuid();

        var updateRes = await client.PutAsJsonAsync($"/api/currencies/{currencyId}", new UpdateCurrencyRequest
        {
            CurrencyName = "After Update",
            Symbol = "A",
            DecimalPlaces = 3
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.ReadDataJsonElementAsync();
        Assert.Equal("After Update", updated.GetProperty("currencyName").GetString());
        Assert.Equal("A", updated.GetProperty("symbol").GetString());
        Assert.Equal(3, updated.GetProperty("decimalPlaces").GetInt32());
    }

    [Fact]
    public async Task DeactivateAndActivateCurrency_TogglesStatus()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "CURRENCY.VIEW", "CURRENCY.CREATE", "CURRENCY.EDIT", "CURRENCY.DELETE" });

        var code = $"T{Guid.NewGuid():N}"[..3].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = "Toggle Currency",
            Symbol = "T"
        });
        var currencyId = (await createRes.ReadDataJsonElementAsync()).GetProperty("currencyId").GetGuid();

        // Deactivate
        var deactRes = await client.DeleteAsync($"/api/currencies/{currencyId}");
        Assert.Equal(HttpStatusCode.NoContent, deactRes.StatusCode);

        var getRes1 = await client.GetAsync($"/api/currencies/{currencyId}");
        var data1 = await getRes1.ReadDataJsonElementAsync();
        Assert.False(data1.GetProperty("isActive").GetBoolean());

        // Activate
        var actRes = await client.PostAsync($"/api/currencies/{currencyId}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, actRes.StatusCode);

        var getRes2 = await client.GetAsync($"/api/currencies/{currencyId}");
        var data2 = await getRes2.ReadDataJsonElementAsync();
        Assert.True(data2.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Currency_WithoutPermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantUserAlphaId,
            "tenantuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant User" },
            new[] { "UOM.VIEW" }); // Lacks CURRENCY permissions

        var getRes = await client.GetAsync("/api/currencies");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        var postRes = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = "EUR",
            CurrencyName = "Euro",
            Symbol = "€"
        });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
    }

    [Fact]
    public async Task PurchaseOrder_HasDefaultCurrencyUSD()
    {
        var po = PurchaseOrder.Create(
            TestConstants.TenantAlphaId,
            TestConstants.CompanyAId,
            TestConstants.BranchA1Id,
            $"PO_CURR_{Guid.NewGuid():N}"[..12]);

        Assert.Equal("USD", po.Currency);
    }
}
