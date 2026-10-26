using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Features.MasterData.PaymentTerm;
using Xunit;

namespace ERP.SecurityTests.MasterData;

[Collection("SecurityTestCollection")]
public class PaymentTermTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;

    public PaymentTermTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreatePaymentTerm_WithValidData_ReturnsCreated()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PAYMENT_TERM.VIEW", "PAYMENT_TERM.CREATE" });

        var code = $"NET{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var name = $"Net 45 {Guid.NewGuid():N}"[..18];
        var response = await client.PostAsJsonAsync("/api/payment-terms", new CreatePaymentTermRequest
        {
            TermCode = code,
            TermName = name,
            Days = 45,
            Description = "Standard commercial terms"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadDataJsonElementAsync();
        Assert.Equal(code, created.GetProperty("termCode").GetString());
        Assert.Equal(name, created.GetProperty("termName").GetString());
        Assert.Equal(45, created.GetProperty("days").GetInt32());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateAndReactivatePaymentTerm_UpdatesStatusCorrectly()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PAYMENT_TERM.VIEW", "PAYMENT_TERM.CREATE", "PAYMENT_TERM.EDIT", "PAYMENT_TERM.DELETE" });

        var code = $"NET{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createRes = await client.PostAsJsonAsync("/api/payment-terms", new CreatePaymentTermRequest
        {
            TermCode = code,
            TermName = $"Term {code}",
            Days = 30
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var termId = (await createRes.ReadDataJsonElementAsync()).GetProperty("paymentTermId").GetString();

        // Deactivate
        var deactRes = await client.DeleteAsync($"/api/payment-terms/{termId}");
        Assert.Equal(HttpStatusCode.NoContent, deactRes.StatusCode);

        var getDeact = await client.GetAsync($"/api/payment-terms/{termId}");
        var deactJson = await getDeact.ReadDataJsonElementAsync();
        Assert.False(deactJson.GetProperty("isActive").GetBoolean());

        // Reactivate
        var reactRes = await client.PostAsync($"/api/payment-terms/{termId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactRes.StatusCode);

        var getReact = await client.GetAsync($"/api/payment-terms/{termId}");
        var reactJson = await getReact.ReadDataJsonElementAsync();
        Assert.True(reactJson.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task PaymentTerm_TenantIsolation_EnforcesStrictBoundaries()
    {
        var clientA = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PAYMENT_TERM.VIEW", "PAYMENT_TERM.CREATE" });

        var code = $"NET{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var createRes = await clientA.PostAsJsonAsync("/api/payment-terms", new CreatePaymentTermRequest
        {
            TermCode = code,
            TermName = $"Term {code}",
            Days = 15
        });
        var termId = (await createRes.ReadDataJsonElementAsync()).GetProperty("paymentTermId").GetString();

        var clientB = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "PAYMENT_TERM.VIEW" });

        var getRes = await clientB.GetAsync($"/api/payment-terms/{termId}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
