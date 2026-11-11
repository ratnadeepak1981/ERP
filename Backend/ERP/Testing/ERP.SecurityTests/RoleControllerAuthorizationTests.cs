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
public class RoleControllerAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RoleControllerAuthorizationTests(TestSecurityFixture fixture)
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
    public async Task Unauthenticated_GetRoles_ReturnsUnauthorized()
    {
        var anonymousClient = _fixture.CreateClient();
        var response = await anonymousClient.GetAsync("/api/role");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithoutRoleView_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Operator" },
            permissions: Array.Empty<string>());

        var response = await client.GetAsync("/api/role");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRoles_WithRoleView_ReturnsOk()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Auditor" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await client.GetAsync("/api/role");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(data);
        Assert.NotEmpty(data);
    }

    [Fact]
    public async Task CreateRole_WithoutRoleCreate_ReturnsForbidden()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Operator" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "Unauthorized New Role " + Guid.NewGuid().ToString("N")[..8],
            Description = "Should be rejected"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithRoleCreate_CreatesRoleWithTenantOwnership()
    {
        var client = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Manager" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var uniqueName = "SecRole_" + Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Custom tenant role created with permission"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(uniqueName, data.GetProperty("name").GetString());
        Assert.Equal(TestConstants.TenantAlphaId, data.GetProperty("tenantId").GetGuid());
        Assert.False(data.GetProperty("isSystemRole").GetBoolean());
    }

    [Fact]
    public async Task GetRoleById_WithoutRoleView_ReturnsForbidden()
    {
        // 1. Create a role as tenant admin
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var createRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "InspectRole_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Role for inspection test"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = createdData.GetProperty("roleId").GetGuid();

        // 2. Access without ROLE.VIEW
        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Operator" },
            permissions: Array.Empty<string>());

        var response = await unprivilegedClient.GetAsync($"/api/role/{roleId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetRoleById_WithRoleView_ReturnsOk()
    {
        // 1. Create a role
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var uniqueName = "InspectRoleOk_" + Guid.NewGuid().ToString("N")[..8];
        var createRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Role for inspection test ok"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = createdData.GetProperty("roleId").GetGuid();

        // 2. View with custom role having ROLE.VIEW
        var viewerClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Viewer" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await viewerClient.GetAsync($"/api/role/{roleId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(uniqueName, data.GetProperty("name").GetString());
        Assert.Equal(roleId.ToString(), data.GetProperty("id").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task UpdateRole_WithoutRoleEdit_ReturnsForbidden()
    {
        // 1. Create role
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var createRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "PreEditRole_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Pre-edit role"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = createdData.GetProperty("roleId").GetGuid();

        // 2. Update without ROLE.EDIT
        var unprivilegedClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Viewer" },
            permissions: new[] { "ROLE.VIEW" });

        var response = await unprivilegedClient.PutAsJsonAsync($"/api/role/{roleId}", new UpdateRoleRequest
        {
            Name = "Updated Name",
            Description = "Updated Desc"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_WithRoleEdit_ReturnsOk()
    {
        // 1. Create role
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var createRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "PreEditRole2_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Pre-edit role 2"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = createdData.GetProperty("roleId").GetGuid();

        // 2. Update with ROLE.EDIT
        var editorClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Custom Editor" },
            permissions: new[] { "ROLE.EDIT" });

        var updatedName = "UpdatedRole_" + Guid.NewGuid().ToString("N")[..8];
        var response = await editorClient.PutAsJsonAsync($"/api/role/{roleId}", new UpdateRoleRequest
        {
            Name = updatedName,
            Description = "Updated by editor"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(updatedName, data.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToggleRoleStatus_Lifecycle_DeactivationRequiresDelete_ReactivationRequiresEdit()
    {
        // 1. Create an active role
        var adminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var createRes = await adminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "ToggleRole_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Role for status toggling"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = createdData.GetProperty("roleId").GetGuid();

        // 2. Attempt deactivation with ROLE.EDIT only -> Expect Forbidden
        var editOnlyClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Editor" },
            permissions: new[] { "ROLE.EDIT" });

        var deactForbiddenRes = await editOnlyClient.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.Forbidden, deactForbiddenRes.StatusCode);

        // 3. Deactivate with ROLE.DELETE -> Expect OK
        var deleteClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Deleter" },
            permissions: new[] { "ROLE.DELETE" });

        var deactOkRes = await deleteClient.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, deactOkRes.StatusCode);
        var deactData = await deactOkRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.False(deactData.GetProperty("isActive").GetBoolean());

        // 4. Attempt reactivation with ROLE.DELETE only -> Expect Forbidden
        var reactForbiddenRes = await deleteClient.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.Forbidden, reactForbiddenRes.StatusCode);

        // 5. Reactivate with ROLE.EDIT -> Expect OK
        var reactOkRes = await editOnlyClient.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, reactOkRes.StatusCode);
        var reactData = await reactOkRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.True(reactData.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CrossTenant_RoleAccess_ReturnsForbidden()
    {
        // 1. Create role in Tenant Alpha
        var alphaAdminClient = CreateClient(
            TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.CREATE" });

        var createRes = await alphaAdminClient.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "AlphaExclusive_" + Guid.NewGuid().ToString("N")[..8],
            Description = "Tenant Alpha exclusive role"
        });
        var createdData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var alphaRoleId = createdData.GetProperty("roleId").GetGuid();

        // 2. Tenant Beta admin attempts to view Tenant Alpha's role
        var betaAdminClient = CreateClient(
            TestConstants.TenantBetaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "ROLE.VIEW", "ROLE.EDIT", "ROLE.DELETE" });

        var viewRes = await betaAdminClient.GetAsync($"/api/role/{alphaRoleId}");
        Assert.Equal(HttpStatusCode.Forbidden, viewRes.StatusCode);

        // 3. Tenant Beta admin attempts to modify Tenant Alpha's role
        var editRes = await betaAdminClient.PutAsJsonAsync($"/api/role/{alphaRoleId}", new UpdateRoleRequest
        {
            Name = "CrossTenantHacked",
            Description = "Malicious update"
        });
        Assert.Equal(HttpStatusCode.Forbidden, editRes.StatusCode);

        // 4. Tenant Beta admin attempts to toggle Tenant Alpha's role
        var toggleRes = await betaAdminClient.PutAsync($"/api/role/{alphaRoleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.Forbidden, toggleRes.StatusCode);
    }
}
