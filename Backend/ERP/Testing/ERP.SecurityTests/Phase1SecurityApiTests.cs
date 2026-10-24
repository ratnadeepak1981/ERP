using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Security.Application.DTOs;
using Xunit;

namespace ERP.SecurityTests;

[Collection("SecurityTestCollection")]
public class Phase1SecurityApiTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public Phase1SecurityApiTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private System.Net.Http.HttpClient CreateTenantAdminAlphaClient()
    {
        return _fixture.CreateAuthenticatedClient(
            userId: TestConstants.TenantAdminAlphaId,
            username: "tenantadmin_a",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "Tenant Admin" },
            permissions: new[] { "COMPANY.VIEW", "USER.VIEW", "ROLE.VIEW" });
    }

    private System.Net.Http.HttpClient CreatePlatformAdminClient()
    {
        return _fixture.CreateAuthenticatedClient(
            userId: TestConstants.PlatformAdminId,
            username: "platformadmin",
            tenantId: null,
            roles: new[] { "Platform Admin" },
            permissions: new[] { "TENANT.VIEW", "SUBSCRIPTION.VIEW", "AUDIT.VIEW" });
    }

    #region 1. Permission Discovery & Isolation Tests

    [Fact]
    public async Task GetPermissions_TenantAdmin_ReturnsPermissionsExcludingPlatformModules()
    {
        var client = CreateTenantAdminAlphaClient();
        var response = await client.GetAsync("/api/permission");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);

        Assert.NotNull(envelope);
        Assert.NotEmpty(envelope);

        // Verify none of the permissions are platform-only (Tenant, Subscription)
        foreach (var p in envelope)
        {
            var code = p.GetProperty("code").GetString();
            var module = p.GetProperty("module").GetString();

            Assert.DoesNotContain("TENANT.", code ?? "");
            Assert.DoesNotContain("SUBSCRIPTION.", code ?? "");
            Assert.NotEqual("Tenant", module);
            Assert.NotEqual("Subscription", module);
        }
    }

    [Fact]
    public async Task GetPermissions_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/permission");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region 2. Role Administration & Isolation Tests

    [Fact]
    public async Task GetRoles_TenantAdmin_ExcludesPlatformRoles()
    {
        var client = CreateTenantAdminAlphaClient();
        var response = await client.GetAsync("/api/role");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);

        Assert.NotNull(roles);
        Assert.NotEmpty(roles);

        // Verify platform roles are not visible to Tenant Admin
        foreach (var r in roles)
        {
            var name = r.GetProperty("name").GetString();
            Assert.NotEqual("Platform Admin", name);
            Assert.NotEqual("Platform Staff", name);
            Assert.NotEqual("Security Manager", name);
        }
    }

    [Fact]
    public async Task CreateRole_TenantAdmin_CreatesRoleWithTenantOwnership()
    {
        var client = CreateTenantAdminAlphaClient();
        var uniqueName = "Procurement Specialist " + Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Custom operational role"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var roleData = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);

        Assert.Equal(uniqueName, roleData.GetProperty("name").GetString());
        Assert.Equal(TestConstants.TenantAlphaId, roleData.GetProperty("tenantId").GetGuid());
        Assert.False(roleData.GetProperty("isSystemRole").GetBoolean());
    }

    [Fact]
    public async Task CreateRole_ReservedPlatformName_ReturnsBadRequest()
    {
        var client = CreateTenantAdminAlphaClient();

        var response = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = "Platform Admin",
            Description = "Attempting to spoof platform role"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRoleById_TenantAdmin_CanViewOwnCreatedRole()
    {
        var client = CreateTenantAdminAlphaClient();
        var uniqueName = "Inventory Auditor " + Guid.NewGuid().ToString("N")[..8];

        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Auditor role"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        var getRes = await client.GetAsync($"/api/role/{roleId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var retrieved = await getRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(uniqueName, retrieved.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateRole_TenantAdmin_CanUpdateOwnCustomRole()
    {
        var client = CreateTenantAdminAlphaClient();
        var uniqueName = "Temp Role " + Guid.NewGuid().ToString("N")[..8];

        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Initial"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        var updatedName = "Updated " + uniqueName;
        var updateRes = await client.PutAsJsonAsync($"/api/role/{roleId}", new UpdateRoleRequest
        {
            Name = updatedName,
            Description = "Updated Description"
        });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var getRes = await client.GetAsync($"/api/role/{roleId}");
        var retrieved = await getRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(updatedName, retrieved.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToggleRoleStatus_TenantAdmin_CanDeactivateAndReactivateRole()
    {
        var client = CreateTenantAdminAlphaClient();
        var uniqueName = "Deactivatable Role " + Guid.NewGuid().ToString("N")[..8];

        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Initial"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        // Deactivate
        var toggle1 = await client.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, toggle1.StatusCode);

        var get1 = await client.GetAsync($"/api/role/{roleId}");
        var data1 = await get1.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.False(data1.GetProperty("isActive").GetBoolean());

        // Reactivate
        var toggle2 = await client.PutAsync($"/api/role/{roleId}/toggle-status", null);
        Assert.Equal(HttpStatusCode.OK, toggle2.StatusCode);

        var get2 = await client.GetAsync($"/api/role/{roleId}");
        var data2 = await get2.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.True(data2.GetProperty("isActive").GetBoolean());
    }

    #endregion

    #region 3. Role-Permission Assignment & Isolation Tests

    [Fact]
    public async Task AssignPermissionToRole_TenantAdmin_CanAssignPermissionToCustomRole()
    {
        var client = CreateTenantAdminAlphaClient();

        // 1. Create a custom role
        var uniqueName = "Custom Buyer " + Guid.NewGuid().ToString("N")[..8];
        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Custom buyer"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        // 2. Discover an allowed permission
        var permRes = await client.GetAsync("/api/permission?module=PurchaseOrder");
        var perms = await permRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(perms);
        Assert.NotEmpty(perms);
        var targetPermId = perms[0].GetProperty("id").GetGuid();

        // 3. Assign permission to role
        var assignRes = await client.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = targetPermId
        });

        Assert.Equal(HttpStatusCode.OK, assignRes.StatusCode);

        // 4. Verify permission is present in role permissions
        var getPermsRes = await client.GetAsync($"/api/rolepermission/{roleId}");
        Assert.Equal(HttpStatusCode.OK, getPermsRes.StatusCode);

        var assigned = await getPermsRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(assigned);
        Assert.Contains(assigned, p => p.GetProperty("id").GetGuid() == targetPermId);
    }

    [Fact]
    public async Task BatchAssignPermissions_TenantAdmin_CanSynchronizePermissions()
    {
        var client = CreateTenantAdminAlphaClient();

        // 1. Create custom role
        var uniqueName = "Batch Role " + Guid.NewGuid().ToString("N")[..8];
        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Batch role"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        // 2. Batch assign by codes
        var batchRes = await client.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionCodes = new List<string> { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" }
        });

        Assert.Equal(HttpStatusCode.OK, batchRes.StatusCode);

        // 3. Verify assigned permissions via rolepermission endpoint
        var getRes = await client.GetAsync($"/api/rolepermission/{roleId}");
        var assigned = await getRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(assigned);

        Assert.Equal(2, assigned.Count);
        Assert.Contains(assigned, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.VIEW");
        Assert.Contains(assigned, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.CREATE");

        // 4. Verify assigned permissions returned by GetRoleById as well
        var roleDetailRes = await client.GetAsync($"/api/role/{roleId}");
        Assert.Equal(HttpStatusCode.OK, roleDetailRes.StatusCode);
        var roleDetail = await roleDetailRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var permsInDetail = roleDetail.GetProperty("permissions").EnumerateArray().ToList();
        Assert.Equal(2, permsInDetail.Count);
        Assert.Contains(permsInDetail, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.VIEW");
    }

    [Fact]
    public async Task BatchAssignPermissions_DeltaPreservesUnchangedAndRemovesDeselected()
    {
        var client = CreateTenantAdminAlphaClient();

        // 1. Create custom role
        var uniqueName = "Delta Role " + Guid.NewGuid().ToString("N")[..8];
        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Delta test role"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        // 2. Initial assign: PURCHASE_ORDER.VIEW and PURCHASE_ORDER.CREATE
        await client.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionCodes = new List<string> { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" }
        });

        // 3. Delta update: keep PURCHASE_ORDER.CREATE, remove PURCHASE_ORDER.VIEW, add PURCHASE_ORDER.APPROVE
        var updateRes = await client.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionCodes = new List<string> { "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.APPROVE" }
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 4. Verify resulting set
        var getRes = await client.GetAsync($"/api/rolepermission/{roleId}");
        var assigned = await getRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(assigned);
        Assert.Equal(2, assigned.Count);

        Assert.Contains(assigned, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.CREATE");
        Assert.Contains(assigned, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.APPROVE");
        Assert.DoesNotContain(assigned, p => p.GetProperty("code").GetString() == "PURCHASE_ORDER.VIEW");
    }

    [Fact]
    public async Task BatchAssignPermissions_EmptyList_ClearsAllPermissions()
    {
        var client = CreateTenantAdminAlphaClient();

        // 1. Create custom role
        var uniqueName = "Empty Batch Role " + Guid.NewGuid().ToString("N")[..8];
        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Empty batch test role"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        // 2. Assign initial permissions
        await client.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionCodes = new List<string> { "PURCHASE_ORDER.VIEW" }
        });

        // 3. Clear all permissions by sending empty lists
        var clearRes = await client.PostAsJsonAsync("/api/rolepermission/batch", new AssignRolePermissionsBatchRequest
        {
            RoleId = roleId,
            PermissionCodes = new List<string>(),
            PermissionIds = new List<string>()
        });
        Assert.Equal(HttpStatusCode.OK, clearRes.StatusCode);

        // 4. Verify 0 assigned permissions
        var getRes = await client.GetAsync($"/api/rolepermission/{roleId}");
        var assigned = await getRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(assigned);
        Assert.Empty(assigned);
    }

    [Fact]
    public async Task PurchaseOrders_ProcurementPermission_EnforcesAccessAccurately()
    {
        // 1. Client with Company Scope AND PURCHASE_ORDER.VIEW permission -> 200 OK
        var authorizedClient = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.CompanyUserAId,
            username: "compuser_a",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "Procurment Manager" },
            permissions: new[] { "PURCHASE_ORDER.VIEW" });

        var authorizedRes = await authorizedClient.GetAsync($"/api/companies/{TestConstants.CompanyAId}/purchase-orders");
        Assert.Equal(HttpStatusCode.OK, authorizedRes.StatusCode);

        // 2. Client with Company Scope but WITHOUT PURCHASE_ORDER.VIEW permission -> 403 Forbidden
        var unauthorizedClient = _fixture.CreateAuthenticatedClient(
            userId: TestConstants.CompanyUserAId,
            username: "compuser_a",
            tenantId: TestConstants.TenantAlphaId,
            roles: new[] { "Procurment Manager" },
            permissions: Array.Empty<string>());

        var unauthorizedRes = await unauthorizedClient.GetAsync($"/api/companies/{TestConstants.CompanyAId}/purchase-orders");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedRes.StatusCode);
    }

    [Fact]
    public async Task RemovePermissionFromRole_TenantAdmin_CanRemoveAssignedPermission()
    {
        var client = CreateTenantAdminAlphaClient();

        // 1. Create role and assign permission
        var uniqueName = "Removable Perm Role " + Guid.NewGuid().ToString("N")[..8];
        var createRes = await client.PostAsJsonAsync("/api/role", new CreateRoleRequest
        {
            Name = uniqueName,
            Description = "Test"
        });
        var roleData = await createRes.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        var roleId = roleData.GetProperty("roleId").GetGuid();

        var permRes = await client.GetAsync("/api/permission?module=Product");
        var perms = await permRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(perms);
        Assert.NotEmpty(perms);
        var permId = perms[0].GetProperty("id").GetGuid();

        await client.PostAsJsonAsync("/api/rolepermission", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permId
        });

        // 2. Remove permission
        var removeRes = await client.PostAsJsonAsync("/api/rolepermission/remove", new AssignPermissionToRoleRequest
        {
            RoleId = roleId,
            PermissionId = permId
        });
        Assert.Equal(HttpStatusCode.OK, removeRes.StatusCode);

        // 3. Verify no longer assigned
        var getRes = await client.GetAsync($"/api/rolepermission/{roleId}");
        var assigned = await getRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(assigned);
        Assert.DoesNotContain(assigned, p => p.GetProperty("id").GetGuid() == permId);
    }

    #endregion

    #region 4. User Administration & Tenant Isolation Tests

    [Fact]
    public async Task GetUserById_TenantAdmin_CanViewOwnTenantUser()
    {
        var client = CreateTenantAdminAlphaClient();

        var response = await client.GetAsync($"/api/user/{TestConstants.TenantUserAlphaId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userData = await response.ReadApiResponseDataAsync<JsonElement>(_jsonOpts);
        Assert.Equal(TestConstants.TenantAlphaId, userData.GetProperty("tenantId").GetGuid());
    }

    [Fact]
    public async Task GetUserById_TenantAdmin_CannotViewOtherTenantUser_ReturnsForbidden()
    {
        var client = CreateTenantAdminAlphaClient();

        // Attempt to access user from Tenant Beta
        var response = await client.GetAsync($"/api/user/{TestConstants.TenantAdminBetaId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignRoleToUser_TenantAdmin_CannotAssignPlatformRole_ReturnsForbidden()
    {
        var client = CreateTenantAdminAlphaClient();

        // Find "Platform Admin" role ID using Platform Admin client
        var platformClient = CreatePlatformAdminClient();
        var rolesRes = await platformClient.GetAsync("/api/role");
        var allRoles = await rolesRes.ReadApiResponseDataAsync<List<JsonElement>>(_jsonOpts);
        Assert.NotNull(allRoles);
        var platformAdminRole = allRoles.Find(r => r.GetProperty("name").GetString() == "Platform Admin");
        var platformRoleId = Guid.Parse(platformAdminRole.GetProperty("id").GetString()!);

        // Tenant Admin attempts to assign Platform Admin role to tenant user
        var assignRes = await client.PostAsJsonAsync("/api/userrole", new AssignRoleToUserRequest
        {
            UserId = TestConstants.TenantUserAlphaId,
            RoleId = platformRoleId
        });

        Assert.Equal(HttpStatusCode.Forbidden, assignRes.StatusCode);
    }

    #endregion
}
