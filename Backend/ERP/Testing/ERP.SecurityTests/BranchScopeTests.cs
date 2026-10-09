using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ERP.SecurityTests;

public class BranchScopeTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public BranchScopeTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetBranches_BranchA1User_CanAccessAssignedBranch()
    {
        // Arrange: User assigned only Company A + Branch A1
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "BRANCH.VIEW" });

        // Act
        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var branch = await response.Content.ReadFromJsonAsync<BranchDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(branch);
        Assert.Equal(TestConstants.BranchA1Id, branch.Id);
    }

    [Fact]
    public async Task GetBranches_BranchA1User_AccessingUnassignedCompany_ReturnsForbidden()
    {
        // Arrange: User has Company A scope, tries to access Company B branches
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "BRANCH.VIEW" });

        // Act
        var response = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}/branches");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBranches_MultiBranchUser_CanAccessBothAssignedBranches()
    {
        // Arrange: User assigned Branch A1 and Branch A2 under Company A
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MultiBranchUserId,
            "multibranch_user",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "BRANCH.VIEW" });

        // Act - Branch A1
        var res1 = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}");
        // Act - Branch A2
        var res2 = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA2Id}");

        // Assert: Both must succeed
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
    }

    [Fact]
    public async Task GetBranches_MixedScopeUser_EnforcesStrictPairing()
    {
        // Arrange: User assigned (Company A, Branch A1) AND (Company B, Branch B2)
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.MixedScopeUserId,
            "mixedscope_user",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "BRANCH.VIEW" });

        // 1. Authorized pairing (Company A, Branch A1) -> Allowed
        var resA1 = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}");
        Assert.Equal(HttpStatusCode.OK, resA1.StatusCode);

        // 2. Authorized pairing (Company B, Branch B2) -> Allowed
        var resB2 = await client.GetAsync($"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB2Id}");
        Assert.Equal(HttpStatusCode.OK, resB2.StatusCode);

        // 3. Unauthorized pairing (Company A, Branch B2 - mixing company with other branch) -> Denied
        var resInvalidPair = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchB2Id}");
        Assert.True(resInvalidPair.StatusCode == HttpStatusCode.NotFound || resInvalidPair.StatusCode == HttpStatusCode.Forbidden);

        // 4. Cross-tenant attempt (Tenant Beta Company C) -> Denied
        var resCrossTenant = await client.GetAsync($"/api/companies/{TestConstants.CompanyCId}/branches/{TestConstants.BranchC1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, resCrossTenant.StatusCode);
    }

    private class BranchDto
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
