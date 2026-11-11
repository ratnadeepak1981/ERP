using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Security.Application.DTOs;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class UserRoleControllerAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UserRoleControllerAuthorizationTests(TestSecurityFixture fixture)
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

    private async Task<Guid> CreateCustomRoleAsync(HttpClient adminClient)
    {
        var roleRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "RoleForUser_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Role for user role testing"
        });
        var roleData = await roleRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        return roleData.GetProperty("roleId").GetGuid();
    }

    [Fact]
    public async Task Unauthenticated_AssignRole_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WithoutUserAssignRole_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var roleId = await CreateCustomRoleAsync(adminClient);

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW", "USER.EDIT" });

        var response = await unprivilegedClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WithUserAssignRole_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var roleId = await CreateCustomRoleAsync(adminClient);

        var delegatorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "USER.ASSIGN_ROLE" });

        var response = await delegatorClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_WithoutUserAssignRole_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var roleId = await CreateCustomRoleAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: new[] { "USER.VIEW", "USER.EDIT" });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/userrole")
        {
            Content = JsonContent.Create(new AssignRoleToUserRequest
            {
                UserId = TestConstants.TenantUserAlphaId,
                RoleId = roleId
            })
        };

        var response = await unprivilegedClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_WithUserAssignRole_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var roleId = await CreateCustomRoleAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        var delegatorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "USER.ASSIGN_ROLE" });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/userrole")
        {
            Content = JsonContent.Create(new AssignRoleToUserRequest
            {
                UserId = TestConstants.TenantUserAlphaId,
                RoleId = roleId
            })
        };

        var response = await delegatorClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRolePost_WithUserAssignRole_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var roleId = await CreateCustomRoleAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        var delegatorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "USER.ASSIGN_ROLE" });

        var response = await delegatorClient.PostAsJsonAsync("/api/userrole/remove", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = roleId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CrossTenant_RoleAssignment_ReturnsForbidden()
    {
        var alphaAdminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "USER.ASSIGN_ROLE" });

        var alphaRoleId = await CreateCustomRoleAsync(alphaAdminClient);

        var betaAdminClient = CreateClient(
            TestConstants.TenantBetaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "USER.ASSIGN_ROLE", "ROLE.VIEW" });

        // 1. Beta admin attempts to assign role to Alpha user
        var assignRes = await betaAdminClient.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = alphaRoleId
        });
        Assert.Equal(HttpStatusCode.Forbidden, assignRes.StatusCode);

        // 2. Beta admin attempts to remove role from Alpha user
        var removeRes = await betaAdminClient.PostAsJsonAsync("/api/userrole/remove", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = alphaRoleId
        });
        Assert.Equal(HttpStatusCode.Forbidden, removeRes.StatusCode);
    }
}
