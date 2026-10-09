using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ERP.SecurityTests;

public class BaselineAuthTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public BaselineAuthTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetProducts_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        _fixture.EnsureDatabasesInitialized();
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithTenantAdminToken_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithPlatformAdminToken_ReturnsForbidden()
    {
        // Arrange
        // Platform user has null tenantId -> must be forbidden from accessing tenant ERP products
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.PlatformAdminId,
            "platformadmin",
            null,
            new[] { "Platform Admin" },
            new[] { "PRODUCT.VIEW" });

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
