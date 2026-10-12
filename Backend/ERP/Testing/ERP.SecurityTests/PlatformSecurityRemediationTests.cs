using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace ERP.SecurityTests;

public class PlatformSecurityRemediationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public PlatformSecurityRemediationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TenantProvision_Anonymous_ReturnsUnauthorized()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tenant/provision", new
        {
            tenantId = Guid.NewGuid(),
            adminUserName = "testadmin",
            adminEmail = "test@example.com",
            adminPassword = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantProvision_TenantUser_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        // Tenant Admin user trying to provision a tenant
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "TENANT.CREATE" });

        var response = await client.PostAsJsonAsync("/api/tenant/provision", new
        {
            tenantId = Guid.NewGuid(),
            adminUserName = "testadmin",
            adminEmail = "test@example.com",
            adminPassword = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantProvision_PlatformUserWithoutPermission_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        // Platform user but missing TENANT.CREATE permission
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.PlatformAdminId,
            "platformadmin",
            null,
            new[] { "Platform Admin" },
            new[] { "SUBSCRIPTION.VIEW" });

        var response = await client.PostAsJsonAsync("/api/tenant/provision", new
        {
            tenantId = Guid.NewGuid(),
            adminUserName = "testadmin",
            adminEmail = "test@example.com",
            adminPassword = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubscriptionPlan_Anonymous_ReturnsUnauthorized()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/SubscriptionPlan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubscriptionPlan_TenantUser_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        // Tenant Admin user trying to access subscription plans
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "SUBSCRIPTION.VIEW" });

        var response = await client.GetAsync("/api/SubscriptionPlan");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubscriptionPlan_PlatformAdmin_ReturnsOk()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.PlatformAdminId,
            "platformadmin",
            null,
            new[] { "Platform Admin" },
            new[] { "SUBSCRIPTION.VIEW" });

        var response = await client.GetAsync("/api/SubscriptionPlan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DiagnosticEndpoint_TenantAdmin_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        var response = await client.GetAsync("/api/security/diagnostic");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserCreation_TenantAdmin_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "USER.CREATE" });

        var response = await client.PostAsJsonAsync("/api/user", new
        {
            username = "malicious_user",
            email = "malicious@test.local",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantProvision_TenantUserClaimingPlatformAdminRole_ReturnsForbidden()
    {
        _fixture.EnsureDatabasesInitialized();
        // A tenant user having or spoofing "Platform Admin" role claim still has TenantId -> must be forbidden
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Platform Admin" },
            new[] { "TENANT.CREATE" });

        var response = await client.PostAsJsonAsync("/api/tenant/provision", new
        {
            tenantId = Guid.NewGuid(),
            adminUserName = "testadmin",
            adminEmail = "test@example.com",
            adminPassword = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
