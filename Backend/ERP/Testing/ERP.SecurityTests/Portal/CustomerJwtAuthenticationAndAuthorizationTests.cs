using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using API.Features.Portal.DTOs;
using Domain.Features.MasterData.Company;
using Domain.Infrastructure.Persistence;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Application.DTOs;
using Security.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests.Portal;

public class CustomerJwtAuthenticationAndAuthorizationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CustomerJwtAuthenticationAndAuthorizationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CustomerLogin_ValidCredentials_IssuesJwtWithCustomerClaimsAndPortalType()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"buyer_{uniqueKey}@acmecorp.com";
        var password = "StrongPassword123";

        // 1. Register customer
        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Acme Supply {uniqueKey} Ltd",
            CompanyRegistrationNumber = $"REG-{uniqueKey}",
            TaxNumber = $"TAX-{uniqueKey}",
            AddressLine1 = "500 Lakeview Terrace",
            City = "Detroit",
            Country = "United States",
            ContactPersonName = $"Alice {uniqueKey}",
            JobTitle = "Purchasing Officer",
            Email = email,
            MobileNumber = $"+1-555-{uniqueKey}",
            Password = password,
            ConfirmPassword = password
        };

        var regResponse = await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // 2. Perform Customer Login
        var loginRequest = new CustomerLoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        Assert.NotNull(loginResult?.Data);

        var data = loginResult.Data;
        Assert.Equal("PASS", data.Status);
        Assert.False(string.IsNullOrWhiteSpace(data.AccessToken));
        Assert.Equal("Bearer", data.TokenType);
        Assert.Equal(TestConstants.TenantAlphaId, data.TenantId);
        Assert.NotEqual(Guid.Empty, data.CustomerId);
        Assert.NotEqual(Guid.Empty, data.ContactId);
        Assert.Equal("CUSTOMER", data.PortalType);
        Assert.Contains("CUSTOMER_COMPANY_ADMIN", data.Roles);
        Assert.Contains("PORTAL_PROFILE.VIEW", data.Permissions);
        Assert.Contains("PORTAL_QUOTATION.ACCEPT", data.Permissions); // Primary contact gets quote accept permission

        // 3. Inspect JWT Token Claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(data.AccessToken);

        Assert.Equal("CSharpAuthServer", jwt.Issuer);
        Assert.Contains("ERP", jwt.Audiences);

        Assert.Equal(data.UserId.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(data.TenantId.ToString(), jwt.Claims.First(c => c.Type == "tenant_id").Value);
        Assert.Equal(data.CustomerId.ToString(), jwt.Claims.First(c => c.Type == "customer_id").Value);
        Assert.Equal(data.ContactId.ToString(), jwt.Claims.First(c => c.Type == "contact_id").Value);
        Assert.Equal("CUSTOMER", jwt.Claims.First(c => c.Type == "portal_type").Value);
        Assert.Contains(jwt.Claims, c => (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role || c.Type.EndsWith("/claims/role")) && c.Value == "CUSTOMER_COMPANY_ADMIN");
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "PORTAL_SALES.VIEW");

        // Verify NO employee permissions exist in the Customer JWT
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "permission" && c.Value == "SALES_ORDER.CREATE");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "permission" && c.Value == "SALES_QUOTATION.APPROVE");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "permission" && c.Value == "INVENTORY.VIEW");
    }

    [Fact]
    public async Task CustomerLogin_InvalidPasswordOrEmail_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"officer_{uniqueKey}@logistics.com";
        var password = "CorrectPassword123";

        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Logistics {uniqueKey} Inc",
            TaxNumber = $"TAX-LOG-{uniqueKey}",
            ContactPersonName = "Bob Officer",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        var regResponse = await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Wrong password
        var wrongPassResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = email,
            Password = "WrongPassword999"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassResponse.StatusCode);

        // Unknown email
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = "nonexistent_buyer@nowhere.com",
            Password = password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerLogin_InactiveUser_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"inactive_{uniqueKey}@corp.com";
        var password = "Password123";

        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Deactivated {uniqueKey} Corp",
            TaxNumber = $"TAX-INACT-{uniqueKey}",
            ContactPersonName = "Deactivated User",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        var regResponse = await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Deactivate user in SecurityDbContext
        using (var scope = _fixture.Services.CreateScope())
        {
            var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
            var user = await securityDb.Users.FirstAsync(u => u.Email == email.ToLowerInvariant());
            user.IsActive = false;
            await securityDb.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ExistingTenantLogin_ContinuesWorkingNormally()
    {
        var client = _fixture.CreateClient();

        // Use the existing tenant admin credentials
        var loginRequest = new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        };

        var response = await client.PostAsJsonAsync("/api/security/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOpts);
        Assert.NotNull(result?.Data);
        Assert.Equal("PASS", result.Data.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.Equal(TestConstants.TenantAlphaId, result.Data.TenantId);
    }

    [Fact]
    public async Task CustomerProfile_AuthenticatedCustomer_ReturnsSanitizedProfile()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"profile_{uniqueKey}@distributor.com";
        var password = "ProfilePassword123";

        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Apex Distributor {uniqueKey} LLC",
            TaxNumber = $"TAX-PROF-{uniqueKey}",
            City = "Denver",
            Country = "United States",
            ContactPersonName = $"Carol {uniqueKey}",
            JobTitle = "VP Purchasing",
            Email = email,
            MobileNumber = $"+1-555-{uniqueKey}",
            Password = password,
            ConfirmPassword = password
        };
        var regResponse = await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        // Login
        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = email,
            Password = password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        var token = loginResult!.Data!.AccessToken;

        // Call GET /api/portal/profile
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/portal/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var profileResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var rawJson = await profileResponse.Content.ReadAsStringAsync();
        // Verify deferred creditLimit and paymentTerms are strictly excluded from customer profile
        Assert.DoesNotContain("creditLimit", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paymentTerms", rawJson, StringComparison.OrdinalIgnoreCase);

        var profileResult = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>(_jsonOpts);
        Assert.NotNull(profileResult?.Data);

        var profile = profileResult.Data;
        Assert.Equal($"Apex Distributor {uniqueKey} LLC", profile.Customer.CustomerName);
        Assert.Equal($"TAX-PROF-{uniqueKey}", profile.Customer.TaxNumber);
        Assert.Equal("Denver", profile.Customer.City);
        Assert.Equal($"Carol {uniqueKey}", profile.Contact.ContactName);
        Assert.Equal("VP Purchasing", profile.Contact.JobTitle);
        Assert.True(profile.Membership.IsPrimary);
        Assert.True(profile.Membership.IsActive);
        Assert.False(profile.Membership.RequiresAuthorization);
        Assert.Contains("CUSTOMER_COMPANY_ADMIN", profile.Membership.Roles);
        Assert.NotNull(profile.AvailableSellingCompanies);
        Assert.NotEmpty(profile.AvailableSellingCompanies);
    }

    [Fact]
    public async Task CustomerToken_AccessingInternalEmployeeApis_IsStrictlyForbiddenOrRejected()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"intruder_{uniqueKey}@outsidecompany.com";
        var password = "IntruderPassword123";

        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Outside Corp {uniqueKey}",
            TaxNumber = $"TAX-OUT-{uniqueKey}",
            ContactPersonName = "Eve Intruder",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = email,
            Password = password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        var customerToken = loginResult!.Data!.AccessToken;

        // Attempt accessing internal employee Sales Orders endpoint
        var soRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders");
        soRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var soResponse = await client.SendAsync(soRequest);
        Assert.Equal(HttpStatusCode.Forbidden, soResponse.StatusCode);

        // Attempt accessing internal employee Sales Quotations endpoint
        var sqRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations");
        sqRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var sqResponse = await client.SendAsync(sqRequest);
        Assert.Equal(HttpStatusCode.Forbidden, sqResponse.StatusCode);

        // Attempt accessing internal employee Purchase Orders endpoint
        var poRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders");
        poRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var poResponse = await client.SendAsync(poRequest);
        Assert.Equal(HttpStatusCode.Forbidden, poResponse.StatusCode);

        // Attempt accessing internal employee Inventory endpoint
        var invRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/inventory/summary");
        invRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var invResponse = await client.SendAsync(invRequest);
        Assert.Equal(HttpStatusCode.Forbidden, invResponse.StatusCode);

        // Attempt accessing internal Tenant Permission management
        var permRequest = new HttpRequestMessage(HttpMethod.Get, "/api/permission");
        permRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        var permResponse = await client.SendAsync(permRequest);
        Assert.Equal(HttpStatusCode.Forbidden, permResponse.StatusCode);
    }

    [Fact]
    public async Task EmployeeToken_AccessingCustomerProfile_IsForbidden()
    {
        var client = _fixture.CreateClient();

        // 1. Login as Tenant Admin (employee token)
        var loginResponse = await client.PostAsJsonAsync("/api/security/login", new LoginRequest
        {
            Email = "admin_a@tenant.local",
            Password = "Password@123"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOpts);
        var employeeToken = loginResult!.Data!.Token;

        // 2. Attempt to call customer portal profile with an employee token
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/portal/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", employeeToken);

        var response = await client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerSellingCompanies_ListingAndSelecting_EnforcesTenantBoundary()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"context_{uniqueKey}@clientcompany.com";
        var password = "ContextPassword123";

        var regRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Client Company {uniqueKey} Ltd",
            TaxNumber = $"TAX-CTX-{uniqueKey}",
            ContactPersonName = "Dave Context",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        await client.PostAsJsonAsync("/api/portal/auth/register", regRequest);

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = email,
            Password = password
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        var customerToken = loginResult!.Data!.AccessToken;

        // 1. List Selling Companies in Tenant Alpha
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/portal/selling-companies");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listResult = await listResponse.Content.ReadFromJsonAsync<ApiResponse<SellingCompanyDto[]>>(_jsonOpts);
        Assert.NotNull(listResult?.Data);
        Assert.Contains(listResult.Data, c => c.CompanyId == TestConstants.CompanyAId);

        // 2. Select valid selling company in Tenant Alpha
        var selectValidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/portal/selling-companies/select")
        {
            Content = JsonContent.Create(new SelectSellingCompanyRequest { SellingCompanyId = TestConstants.CompanyAId })
        };
        selectValidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var selectValidResponse = await client.SendAsync(selectValidRequest);
        Assert.Equal(HttpStatusCode.OK, selectValidResponse.StatusCode);

        // 3. Attempt selecting a company from Tenant Beta (Cross-tenant violation)
        var selectCrossTenantRequest = new HttpRequestMessage(HttpMethod.Post, "/api/portal/selling-companies/select")
        {
            Content = JsonContent.Create(new SelectSellingCompanyRequest { SellingCompanyId = TestConstants.CompanyCId })
        };
        selectCrossTenantRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);

        var selectCrossTenantResponse = await client.SendAsync(selectCrossTenantRequest);
        Assert.Equal(HttpStatusCode.BadRequest, selectCrossTenantResponse.StatusCode);
    }

    [Fact]
    public async Task DeduplicatedNonPrimaryContact_DoesNotGainPurchasingPermissions()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var taxNumber = $"TAX-SHARED-{uniqueKey}";

        // 1. First registration creates new customer company
        var firstRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Shared Company {uniqueKey} Corp",
            TaxNumber = taxNumber,
            ContactPersonName = "Primary Officer",
            Email = $"primary_{uniqueKey}@sharedcorp.com",
            Password = "Password123",
            ConfirmPassword = "Password123"
        };
        var firstResponse = await client.PostAsJsonAsync("/api/portal/auth/register", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // 2. Second registration matches existing company via TaxNumber (Non-primary / RequiresAuthorization)
        var secondEmail = $"secondary_{uniqueKey}@sharedcorp.com";
        var secondRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Shared Company {uniqueKey} Corp",
            TaxNumber = taxNumber,
            ContactPersonName = "Secondary Contact",
            Email = secondEmail,
            Password = "Password123",
            ConfirmPassword = "Password123"
        };
        var secondResponse = await client.PostAsJsonAsync("/api/portal/auth/register", secondRequest);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var secondRegContent = await secondResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerRegistrationResult>>(_jsonOpts);
        Assert.True(secondRegContent!.Data!.IsExistingCompanyMatched);
        Assert.True(secondRegContent.Data.RequiresAuthorization);

        // 3. Second contact logs in
        var secondLoginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", new CustomerLoginRequest
        {
            Email = secondEmail,
            Password = "Password123"
        });
        Assert.Equal(HttpStatusCode.OK, secondLoginResponse.StatusCode);

        var secondLoginResult = await secondLoginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        Assert.NotNull(secondLoginResult?.Data);

        // Non-primary contact MUST NOT have quote accept or order submit permissions!
        Assert.DoesNotContain("PORTAL_QUOTATION.ACCEPT", secondLoginResult.Data.Permissions);
        Assert.DoesNotContain("PORTAL_ORDER.SUBMIT", secondLoginResult.Data.Permissions);
        // But CAN view profile and sales
        Assert.Contains("PORTAL_PROFILE.VIEW", secondLoginResult.Data.Permissions);
        Assert.Contains("PORTAL_SALES.VIEW", secondLoginResult.Data.Permissions);

        // 4. Verify profile reflects RequiresAuthorization == true
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/portal/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondLoginResult.Data.AccessToken);
        var profileResponse = await client.SendAsync(profileRequest);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profileResult = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>(_jsonOpts);
        Assert.False(profileResult!.Data!.Membership.IsPrimary);
        Assert.True(profileResult.Data.Membership.RequiresAuthorization);
    }
}
