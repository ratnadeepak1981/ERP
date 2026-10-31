using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.Currency;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class SupplierAndCustomerCurrencyIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public SupplierAndCustomerCurrencyIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private System.Net.Http.HttpClient CreateTenantAlphaClient()
    {
        return _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "SUPPLIER.VIEW", "SUPPLIER.CREATE", "SUPPLIER.EDIT",
                "CUSTOMER.VIEW", "CUSTOMER.CREATE", "CUSTOMER.EDIT",
                "CURRENCY.VIEW", "CURRENCY.CREATE", "CURRENCY.EDIT"
            });
    }

    private System.Net.Http.HttpClient CreateTenantBetaClient()
    {
        return _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[]
            {
                "SUPPLIER.VIEW", "SUPPLIER.CREATE", "SUPPLIER.EDIT",
                "CUSTOMER.VIEW", "CUSTOMER.CREATE", "CUSTOMER.EDIT",
                "CURRENCY.VIEW", "CURRENCY.CREATE", "CURRENCY.EDIT"
            });
    }

    private async Task<Guid> EnsureCurrencyAsync(System.Net.Http.HttpClient client, string code, string name, string symbol, bool active = true)
    {
        var existingResp = await client.GetAsync("/api/currencies");
        if (existingResp.IsSuccessStatusCode)
        {
            var list = await existingResp.ReadDataJsonElementAsync();
            foreach (var item in list.EnumerateArray())
            {
                if (string.Equals(item.GetProperty("currencyCode").GetString(), code, StringComparison.OrdinalIgnoreCase))
                {
                    return item.GetProperty("currencyId").GetGuid();
                }
            }
        }

        var createResp = await client.PostAsJsonAsync("/api/currencies", new CreateCurrencyRequest
        {
            CurrencyCode = code,
            CurrencyName = name,
            Symbol = symbol,
            DecimalPlaces = 2
        });

        Assert.True(createResp.IsSuccessStatusCode, $"Failed to seed currency {code}: {createResp.StatusCode}");
        var created = await createResp.ReadDataJsonElementAsync();
        var id = created.GetProperty("currencyId").GetGuid();

        if (!active)
        {
            var deactResp = await client.PatchAsync($"/api/currencies/{id}/deactivate", null);
            Assert.True(deactResp.IsSuccessStatusCode);
        }

        return id;
    }

    [Fact]
    public async Task SupplierCurrency_FullLifecycle_MeetsAllTenRequirements()
    {
        var clientA = CreateTenantAlphaClient();
        var clientB = CreateTenantBetaClient();

        var lkrId = await EnsureCurrencyAsync(clientA, "LKR", "Sri Lankan Rupee", "Rs.");
        var usdId = await EnsureCurrencyAsync(clientA, "USD", "US Dollar", "$");
        var inactId = await EnsureCurrencyAsync(clientA, "XIN", "Inactive Coin", "X", active: false);
        var betaCurrId = await EnsureCurrencyAsync(clientB, "EUR", "Euro", "€");

        // 1. Create without currency
        var code1 = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp1 = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = code1,
            SupplierName = $"Supplier No Curr {code1}",
            City = "Colombo",
            CurrencyId = null
        });
        Assert.Equal(HttpStatusCode.Created, createResp1.StatusCode);
        var s1 = await createResp1.ReadDataJsonElementAsync();
        Assert.True(s1.GetProperty("currencyId").ValueKind == JsonValueKind.Null);
        var s1Id = s1.GetProperty("supplierId").GetGuid();

        // 2. Create with LKR
        var code2 = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp2 = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = code2,
            SupplierName = $"Supplier LKR {code2}",
            City = "Colombo",
            CurrencyId = lkrId
        });
        Assert.Equal(HttpStatusCode.Created, createResp2.StatusCode);
        var s2 = await createResp2.ReadDataJsonElementAsync();
        Assert.Equal(lkrId, s2.GetProperty("currencyId").GetGuid());
        Assert.Equal("LKR", s2.GetProperty("currencyCode").GetString());

        // 3. Create with USD
        var code3 = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp3 = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = code3,
            SupplierName = $"Supplier USD {code3}",
            City = "Colombo",
            CurrencyId = usdId
        });
        Assert.Equal(HttpStatusCode.Created, createResp3.StatusCode);
        var s3 = await createResp3.ReadDataJsonElementAsync();
        Assert.Equal(usdId, s3.GetProperty("currencyId").GetGuid());
        Assert.Equal("USD", s3.GetProperty("currencyCode").GetString());
        var s3Id = s3.GetProperty("supplierId").GetGuid();

        // 4. Edit and change currency (USD -> LKR)
        var editResp = await clientA.PutAsJsonAsync($"/api/suppliers/{s3Id}", new UpdateSupplierRequest
        {
            SupplierName = $"Supplier USD {code3} Updated",
            City = "Colombo",
            CurrencyId = lkrId
        });
        Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);
        var s3Updated = await editResp.ReadDataJsonElementAsync();
        Assert.Equal(lkrId, s3Updated.GetProperty("currencyId").GetGuid());
        Assert.Equal("LKR", s3Updated.GetProperty("currencyCode").GetString());

        // 5. Clear currency (LKR -> null)
        var clearResp = await clientA.PutAsJsonAsync($"/api/suppliers/{s3Id}", new UpdateSupplierRequest
        {
            SupplierName = $"Supplier USD {code3} Cleared",
            City = "Colombo",
            CurrencyId = null
        });
        Assert.Equal(HttpStatusCode.OK, clearResp.StatusCode);
        var s3Cleared = await clearResp.ReadDataJsonElementAsync();
        Assert.True(s3Cleared.GetProperty("currencyId").ValueKind == JsonValueKind.Null);

        // 6. Save and reload
        var reloadResp = await clientA.GetAsync($"/api/suppliers/{s3Id}");
        Assert.Equal(HttpStatusCode.OK, reloadResp.StatusCode);
        var s3Reloaded = await reloadResp.ReadDataJsonElementAsync();
        Assert.True(s3Reloaded.GetProperty("currencyId").ValueKind == JsonValueKind.Null);

        // 7. Reject inactive currency assignment
        var inactResp = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            SupplierName = "Inactive Currency Supplier",
            City = "Colombo",
            CurrencyId = inactId
        });
        Assert.False(inactResp.IsSuccessStatusCode);

        // 8. Reject invalid CurrencyId
        var nonExistentId = Guid.NewGuid();
        var invalidResp = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            SupplierName = "Invalid Currency Supplier",
            City = "Colombo",
            CurrencyId = nonExistentId
        });
        Assert.False(invalidResp.IsSuccessStatusCode);

        // 9. Reject cross-tenant CurrencyId (Beta currency assigned in Alpha)
        var crossTenantResp = await clientA.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"S{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            SupplierName = "Cross Tenant Supplier",
            City = "Colombo",
            CurrencyId = betaCurrId
        });
        Assert.False(crossTenantResp.IsSuccessStatusCode);

        // 10. Confirm existing records remain accessible
        var listResp = await clientA.GetAsync("/api/suppliers");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.ReadDataJsonElementAsync();
        Assert.True(list.GetArrayLength() > 0);
    }

    [Fact]
    public async Task CustomerCurrency_FullLifecycle_MeetsAllTenRequirements()
    {
        var clientA = CreateTenantAlphaClient();
        var clientB = CreateTenantBetaClient();

        var lkrId = await EnsureCurrencyAsync(clientA, "LKR", "Sri Lankan Rupee", "Rs.");
        var usdId = await EnsureCurrencyAsync(clientA, "USD", "US Dollar", "$");
        var inactId = await EnsureCurrencyAsync(clientA, "XIN", "Inactive Coin", "X", active: false);
        var betaCurrId = await EnsureCurrencyAsync(clientB, "EUR", "Euro", "€");

        // 1. Create without currency
        var code1 = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp1 = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = code1,
            CustomerName = $"Customer No Curr {code1}",
            City = "Colombo",
            CurrencyId = null
        });
        Assert.Equal(HttpStatusCode.Created, createResp1.StatusCode);
        var c1 = await createResp1.ReadDataJsonElementAsync();
        Assert.True(c1.GetProperty("currencyId").ValueKind == JsonValueKind.Null);

        // 2. Create with LKR
        var code2 = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp2 = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = code2,
            CustomerName = $"Customer LKR {code2}",
            City = "Colombo",
            CurrencyId = lkrId
        });
        Assert.Equal(HttpStatusCode.Created, createResp2.StatusCode);
        var c2 = await createResp2.ReadDataJsonElementAsync();
        Assert.Equal(lkrId, c2.GetProperty("currencyId").GetGuid());
        Assert.Equal("LKR", c2.GetProperty("currencyCode").GetString());

        // 3. Create with USD
        var code3 = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createResp3 = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = code3,
            CustomerName = $"Customer USD {code3}",
            City = "Colombo",
            CurrencyId = usdId
        });
        Assert.Equal(HttpStatusCode.Created, createResp3.StatusCode);
        var c3 = await createResp3.ReadDataJsonElementAsync();
        Assert.Equal(usdId, c3.GetProperty("currencyId").GetGuid());
        Assert.Equal("USD", c3.GetProperty("currencyCode").GetString());
        var c3Id = c3.GetProperty("customerId").GetGuid();

        // 4. Edit and change currency (USD -> LKR)
        var editResp = await clientA.PutAsJsonAsync($"/api/customers/{c3Id}", new UpdateCustomerRequest
        {
            CustomerName = $"Customer USD {code3} Updated",
            City = "Colombo",
            CurrencyId = lkrId
        });
        Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);
        var c3Updated = await editResp.ReadDataJsonElementAsync();
        Assert.Equal(lkrId, c3Updated.GetProperty("currencyId").GetGuid());
        Assert.Equal("LKR", c3Updated.GetProperty("currencyCode").GetString());

        // 5. Clear currency (LKR -> null)
        var clearResp = await clientA.PutAsJsonAsync($"/api/customers/{c3Id}", new UpdateCustomerRequest
        {
            CustomerName = $"Customer USD {code3} Cleared",
            City = "Colombo",
            CurrencyId = null
        });
        Assert.Equal(HttpStatusCode.OK, clearResp.StatusCode);
        var c3Cleared = await clearResp.ReadDataJsonElementAsync();
        Assert.True(c3Cleared.GetProperty("currencyId").ValueKind == JsonValueKind.Null);

        // 6. Save and reload
        var reloadResp = await clientA.GetAsync($"/api/customers/{c3Id}");
        Assert.Equal(HttpStatusCode.OK, reloadResp.StatusCode);
        var c3Reloaded = await reloadResp.ReadDataJsonElementAsync();
        Assert.True(c3Reloaded.GetProperty("currencyId").ValueKind == JsonValueKind.Null);

        // 7. Reject inactive currency assignment
        var inactResp = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            CustomerName = "Inactive Currency Customer",
            City = "Colombo",
            CurrencyId = inactId
        });
        Assert.False(inactResp.IsSuccessStatusCode);

        // 8. Reject invalid CurrencyId
        var nonExistentId = Guid.NewGuid();
        var invalidResp = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            CustomerName = "Invalid Currency Customer",
            City = "Colombo",
            CurrencyId = nonExistentId
        });
        Assert.False(invalidResp.IsSuccessStatusCode);

        // 9. Reject cross-tenant CurrencyId (Beta currency assigned in Alpha)
        var crossTenantResp = await clientA.PostAsJsonAsync("/api/customers", new CreateCustomerRequest
        {
            CustomerCode = $"C{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            CustomerName = "Cross Tenant Customer",
            City = "Colombo",
            CurrencyId = betaCurrId
        });
        Assert.False(crossTenantResp.IsSuccessStatusCode);

        // 10. Confirm existing records remain accessible
        var listResp = await clientA.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.ReadDataJsonElementAsync();
        Assert.True(list.GetArrayLength() > 0);
    }
}
