using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ERP.SecurityTests;

public class CompanyScopeTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public CompanyScopeTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetCompanies_TenantAdmin_CanSeeAllCompaniesInTenant()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "COMPANY.VIEW" });

        // Act
        var response = await client.GetAsync("/api/companies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var companies = await response.Content.ReadFromJsonAsync<List<CompanyDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(companies);
        Assert.Contains(companies, c => c.Id == TestConstants.CompanyAId);
        Assert.Contains(companies, c => c.Id == TestConstants.CompanyBId);
        Assert.DoesNotContain(companies, c => c.Id == TestConstants.CompanyCId); // Tenant Beta company
    }

    [Fact]
    public async Task GetCompanies_CompanyAUser_SeesOnlyCompanyA()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "compuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "COMPANY.VIEW" });

        // Act
        var response = await client.GetAsync("/api/companies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var companies = await response.Content.ReadFromJsonAsync<List<CompanyDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(companies);
        Assert.Contains(companies, c => c.Id == TestConstants.CompanyAId);
        Assert.DoesNotContain(companies, c => c.Id == TestConstants.CompanyBId); // Unassigned
        Assert.DoesNotContain(companies, c => c.Id == TestConstants.CompanyCId); // Other tenant
    }

    [Fact]
    public async Task GetCompanyById_CompanyAUserRequestingCompanyB_ReturnsForbidden()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "compuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "COMPANY.VIEW" });

        // Act: User with Company A scope tries to access Company B directly
        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}");

        // Assert: Access must be strictly denied
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected Forbidden or NotFound, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetCompanyById_TenantAdminARequestingCompanyC_ReturnsForbiddenOrNotFound()
    {
        // Arrange: Tenant Admin A tries to access Company C belonging to Tenant Beta
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "COMPANY.VIEW" });

        // Act
        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyCId}");

        // Assert: Cross-tenant access is strictly denied
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected Forbidden or NotFound, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetCompanies_MultiCompanyUser_CanAccessBothAssignedCompanies()
    {
        // Scenario 1: User assigned to Companies A and B can access both companies
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MultiCompanyUserABId,
            "multicomp_user_ab",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "COMPANY.VIEW" });

        // Act 1: List all accessible companies
        var response = await client.GetAsync("/api/companies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var companies = await response.Content.ReadFromJsonAsync<List<CompanyDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(companies);
        Assert.Contains(companies, c => c.Id == TestConstants.CompanyAId);
        Assert.Contains(companies, c => c.Id == TestConstants.CompanyBId);
        Assert.DoesNotContain(companies, c => c.Id == TestConstants.CompanyCId);

        // Act 2 & 3: Direct access to Company A and Company B
        var resA = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}");
        var resB = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}");

        Assert.Equal(HttpStatusCode.OK, resA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resB.StatusCode);
    }

    [Fact]
    public async Task GetCompanyById_MultiCompanyUser_DeniedAccessToCompanyC()
    {
        // Scenario 2: The same multi-company user is denied access to Company C
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MultiCompanyUserABId,
            "multicomp_user_ab",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "COMPANY.VIEW" });

        // Act: Attempt to directly query Company C
        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyCId}");

        // Assert: Access is denied (403 Forbidden or 404 Not Found)
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected Forbidden or NotFound for Company C, got {response.StatusCode}");
    }

    private class CompanyDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
