using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Features.Domain;
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
}
