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
public class RolePermissionControllerAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RolePermissionControllerAuthorizationTests(TestSecurityFixture fixture)
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

    private async Task<(Guid roleId, Guid permissionId)> CreateCustomRoleAndGetPermissionAsync(HttpClient adminClient)
    {
        var roleRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "PermTestRole_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Role for permission tests"
        });
        var roleData = await roleRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        var permsRes = await adminClient.GetAsync("/api/permission");
        var perms = await permsRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        var permissionId = Guid.Parse(perms[0].GetProperty("id").GetString()!);

        return (roleId, permissionId);
    }

    [Fact]
    public async Task Unauthenticated_GetRolePermissions_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/rolepermission/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRolePermissions_WithoutRoleView_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, _) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Viewer" },
            permissions: Array.Empty<string>());

        var response = await unprivilegedClient.GetAsync($"/api/rolepermission/{roleId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRolePermissions_WithRoleView_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, _) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var viewerClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Auditor" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await viewerClient.GetAsync($"/api/rolepermission/{roleId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(data);
    }

    [Fact]
    public async Task AssignPermissionToRole_WithoutRoleEdit_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Auditor" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await unprivilegedClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignPermissionToRole_WithRoleEdit_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT" });

        var response = await editorClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemovePermissionFromRole_WithoutRoleEdit_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Auditor" },
            permissions: new[] { "ROLE.VIEW" });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/rolepermission")
        {
            Content = JsonContent.Create(new AssignPermissionToRoleRequest
            {
                RoleId = roleId,
                PermissionId = permissionId
            })
        };

        var response = await unprivilegedClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemovePermissionFromRole_WithRoleEdit_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT" });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/rolepermission")
        {
            Content = JsonContent.Create(new AssignPermissionToRoleRequest
            {
                RoleId = roleId,
                PermissionId = permissionId
            })
        };

        var response = await editorClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemovePermissionFromRolePost_WithRoleEdit_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        await adminClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT" });

        var response = await editorClient.PostAsJsonAsync("/api/rolepermission/remove", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permissionId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BatchAssignPermissions_WithoutRoleEdit_ReturnsForbidden()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Auditor" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await unprivilegedClient.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionIds = new List<string> { permissionId.ToString() }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BatchAssignPermissions_WithRoleEdit_ReturnsOk()
    {
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (roleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(adminClient);

        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Security Coordinator" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT" });

        var response = await editorClient.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionIds = new List<string> { permissionId.ToString() }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(1, data.GetProperty("assignedCount").GetInt32());
    }

    [Fact]
    public async Task CrossTenant_RolePermissionAssignment_ReturnsForbidden()
    {
        // 1. Create role in Tenant Alpha
        var alphaAdminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE", "ROLE.EDIT" });

        var (alphaRoleId, permissionId) = await CreateCustomRoleAndGetPermissionAsync(alphaAdminClient);

        // 2. Tenant Beta user with ROLE.EDIT attempts to assign permission to Tenant Alpha's role
        var betaClient = CreateClient(
            TestConstants.TenantBetaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT" });

        var assignRes = await betaClient.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = alphaRoleId,
            PermissionId = permissionId
        });
        Assert.Equal(HttpStatusCode.Forbidden, assignRes.StatusCode);

        // 3. Tenant Beta attempts to view Tenant Alpha's role permissions
        var viewRes = await betaClient.GetAsync($"/api/rolepermission/{alphaRoleId}");
        Assert.Equal(HttpStatusCode.Forbidden, viewRes.StatusCode);

        // 4. Tenant Beta attempts batch assignment
        var batchRes = await betaClient.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = alphaRoleId,
            PermissionIds = new List<string> { permissionId.ToString() }
        });
        Assert.Equal(HttpStatusCode.Forbidden, batchRes.StatusCode);
    }
}
