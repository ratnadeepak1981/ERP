using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Features.Domain;
using Security.Bootstrap.Roles;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class MasterDataEndpointAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MasterDataEndpointAuthorizationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private HttpClient CreateClient(Guid? tenantId, string[] roles, string[] permissions)
    {
        return _fixture.CreateAuthenticatedClient(
            userId: Guid.NewGuid(),
            username: $"user_{Guid.NewGuid():N}"[..16],
            tenantId: tenantId,
            roles: roles,
            permissions: permissions);
    }

    [Fact]
    public async Task Unauthenticated_GetCompanies_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.GetAsync("/api/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_GetBranches_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_CreateBranch_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync($"/api/companies/{TestConstants.CompanyAId}/branches", new CreateBranchRequest
        {
            Name = "Anon Branch",
            Code = "ANON01"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCompany_WithoutCompanyView_ReturnsForbidden()
    {
        // Assigned user with no COMPANY.VIEW permission
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            permissions: new[] { "BRANCH.VIEW" });

        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBranch_WithoutBranchView_ReturnsForbidden()
    {
        // Assigned user with no BRANCH.VIEW permission
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            permissions: new[] { "COMPANY.VIEW" });

        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBranch_WithoutBranchCreate_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            permissions: new[] { "BRANCH.VIEW", "COMPANY.VIEW" });

        var response = await client.PostAsJsonAsync($"/api/companies/{TestConstants.CompanyAId}/branches", new CreateBranchRequest
        {
            Name = "Forbidden Branch",
            Code = "FORBID01"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBranch_WithBranchCreate_CreatesAndPersistsBranchSuccessfully()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "BRANCH.CREATE", "BRANCH.VIEW", "COMPANY.VIEW" });

        var uniqueCode = "BR_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var branchName = "New Branch " + uniqueCode;
        var response = await client.PostAsJsonAsync($"/api/companies/{TestConstants.CompanyAId}/branches", new CreateBranchRequest
        {
            Name = branchName,
            Code = uniqueCode
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var createdId = data.GetProperty("id").GetGuid();
        Assert.Equal(uniqueCode, data.GetProperty("code").GetString());
        Assert.Equal(TestConstants.CompanyAId, data.GetProperty("companyId").GetGuid());
        Assert.True(data.GetProperty("isActive").GetBoolean());

        // Verify persistence: retrieve the branch by ID
        var getResponse = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{createdId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrievedData = await getResponse.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(createdId, retrievedData.GetProperty("id").GetGuid());
        Assert.Equal(uniqueCode, retrievedData.GetProperty("code").GetString());
        Assert.Equal(branchName, retrievedData.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateBranch_CrossTenantCompany_ReturnsForbidden()
    {
        // Tenant Beta admin attempts to create branch in Tenant Alpha's Company A
        var betaClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "BRANCH.CREATE", "BRANCH.VIEW", "COMPANY.VIEW" });

        var response = await betaClient.PostAsJsonAsync($"/api/companies/{TestConstants.CompanyAId}/branches", new CreateBranchRequest
        {
            Name = "Cross Tenant Branch",
            Code = "CROSS01"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // =====================================================================
    // Tenant-boundary master data read baseline
    // =====================================================================

    private static string[] PermissionsFor(string templateKey)
    {
        return StandardRoleTemplates.Find(templateKey)!.PermissionCodes.ToArray();
    }

    [Fact]
    public async Task ProcurementOfficerBaseline_CanReadTransactionLookups()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            new[] { "Procurement Officer" },
            PermissionsFor("PROCUREMENT_OFFICER"));

        var endpoints = new[]
        {
            "/api/currencies",
            "/api/uom",
            "/api/tax-codes?includeInactive=true&applicability=Purchase",
            "/api/master-data/price-cards?type=1&isActive=true&pageSize=100"
        };

        foreach (var endpoint in endpoints)
        {
            var response = await client.GetAsync(endpoint);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 for '{endpoint}' with the Procurement Officer baseline but got " +
                $"{(int)response.StatusCode} {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task SalesOfficerBaseline_CanReadTaxCodesAndCurrencies()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            PermissionsFor("SALES_OFFICER"));

        foreach (var endpoint in new[]
                 {
                     "/api/tax-codes?includeInactive=true&applicability=Sales",
                     "/api/currencies",
                     "/api/uom"
                 })
        {
            var response = await client.GetAsync(endpoint);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 for '{endpoint}' with the Sales Officer baseline but got " +
                $"{(int)response.StatusCode} {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task MasterDataBaseline_DoesNotGrantWriteOperations()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            new[] { "Procurement Officer" },
            PermissionsFor("PROCUREMENT_OFFICER"));

        // Read access must not imply maintenance access.
        var createCurrency = await client.PostAsJsonAsync("/api/currencies", new
        {
            currencyCode = "ZZZ",
            currencyName = "Forbidden Currency",
            symbol = "Z",
            decimalPlaces = 2
        });

        Assert.Equal(HttpStatusCode.Forbidden, createCurrency.StatusCode);
    }

    [Fact]
    public async Task LookupWithoutViewPermission_StillReturnsForbidden()
    {
        // Product-only permission set: no lookup view permissions at all.
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            new[] { "Custom" },
            new[] { "PRODUCT.VIEW" });

        foreach (var endpoint in new[]
                 {
                     "/api/currencies",
                     "/api/uom",
                     "/api/tax-codes",
                     "/api/master-data/price-cards"
                 })
        {
            var response = await client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task MasterDataLookupBaseline_DoesNotLeakAcrossTenants()
    {
        // A Tenant Beta user holding the full Procurement Officer baseline must
        // only see Tenant Beta master data through lookup endpoints.
        var betaClient = CreateClient(
            TestConstants.TenantBetaId,
            new[] { "Procurement Officer" },
            PermissionsFor("PROCUREMENT_OFFICER"));

        var response = await betaClient.GetAsync("/api/suppliers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadDataJsonElementAsync(_jsonOpts);
        Assert.Equal(JsonValueKind.Array, data.ValueKind);

        var supplierCodes = data.EnumerateArray()
            .Select(item => item.TryGetProperty("supplierCode", out var code) ? code.GetString() : null)
            .Where(code => code != null)
            .ToList();

        Assert.Contains("SUPP_BETA", supplierCodes);
        Assert.DoesNotContain("SUPP_ALPHA", supplierCodes);
    }
}
