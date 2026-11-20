using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using API.Features.Portal.DTOs;
using Domain.Infrastructure.Persistence;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Application.DTOs;
using Security.Infrastructure.Persistence;
using Xunit;

namespace ERP.SecurityTests.Portal;

public class CustomerRegistrationIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CustomerRegistrationIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task RegisterCompanyCustomer_ValidRequest_CreatesCustomerAndContact_AndUserCanLoginImmediately()
    {
        var client = _fixture.CreateClient();

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"procurement_{uniqueKey}@testcompany.com";
        var companyName = $"Industrial Supplies {uniqueKey} Corp";
        var password = "SecurePassword123";

        var request = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = companyName,
            TradingName = "Industrial Supplies",
            CompanyRegistrationNumber = $"REG-{uniqueKey}",
            TaxNumber = $"TAX-{uniqueKey}",
            CompanyEmail = $"info_{uniqueKey}@testcompany.com",
            CompanyPhone = $"+1-555-{uniqueKey}",
            AddressLine1 = "100 Industrial Parkway",
            City = "Chicago",
            Country = "United States",
            ContactPersonName = $"John Doe {uniqueKey}",
            JobTitle = "Procurement Director",
            Email = email,
            MobileNumber = $"+1-555-{uniqueKey}",
            Password = password,
            ConfirmPassword = password
        };

        // 1. Submit Registration
        var response = await client.PostAsJsonAsync("/api/portal/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerRegistrationResult>>(_jsonOpts);
        Assert.NotNull(content?.Data);

        var regResult = content.Data;
        Assert.NotEqual(Guid.Empty, regResult.CustomerId);
        Assert.NotEqual(Guid.Empty, regResult.UserId);
        Assert.Equal(email.ToLowerInvariant(), regResult.Email);
        Assert.False(regResult.IsExistingCompanyMatched);
        Assert.False(regResult.RequiresAuthorization);
        Assert.True(regResult.CanLoginImmediately);

        // 2. Verify Database Records (Domain & Security)
        using (var scope = _fixture.Services.CreateScope())
        {
            var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

            var customer = await domainDb.Customers.FirstOrDefaultAsync(c => c.CustomerId == regResult.CustomerId);
            Assert.NotNull(customer);
            Assert.Equal(companyName, customer.CustomerName);
            Assert.Equal(TestConstants.TenantAlphaId, customer.TenantId);
            Assert.True(customer.IsActive);

            var contact = await domainDb.Contacts.FirstOrDefaultAsync(c => c.ContactId == regResult.ContactId);
            Assert.NotNull(contact);
            Assert.Equal(email.ToLowerInvariant(), contact.Email);
            Assert.Equal(TestConstants.TenantAlphaId, contact.TenantId);

            var customerContact = await domainDb.CustomerContacts.FirstOrDefaultAsync(cc => cc.CustomerId == regResult.CustomerId && cc.ContactId == contact.ContactId);
            Assert.NotNull(customerContact);
            Assert.True(customerContact.IsPrimary);
            Assert.True(customerContact.IsActive);

            var user = await securityDb.Users.FirstOrDefaultAsync(u => u.Id == regResult.UserId);
            Assert.NotNull(user);
            Assert.Equal(email.ToLowerInvariant(), user.Email);
            Assert.NotEqual(password, user.PasswordHash); // Password must be hashed!
            Assert.True(user.IsActive); // Must be active immediately in Phase 1!

            var userRole = await securityDb.UserRoles
                .Include(ur => ur.Role)
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id);
            Assert.NotNull(userRole);
            Assert.Equal("CUSTOMER_COMPANY_ADMIN", userRole.Role.Name);

            // Verify customer role does NOT have internal employee permissions
            var permissions = await securityDb.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId == userRole.RoleId)
                .Select(rp => rp.Permission.Code)
                .ToListAsync();

            Assert.DoesNotContain("SALES_QUOTATION.CREATE", permissions);
            Assert.DoesNotContain("SALES_QUOTATION.CONFIRM", permissions);
            Assert.DoesNotContain("SALES_ORDER.CONFIRM", permissions);
        }

        // 3. Verify Immediate Login Eligibility (Phase 1)
        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(_jsonOpts);
        Assert.NotNull(loginContent?.Data);
        Assert.Equal("PASS", loginContent.Data.Status);
        Assert.False(string.IsNullOrWhiteSpace(loginContent.Data.Token));
        Assert.Contains("CUSTOMER_COMPANY_ADMIN", loginContent.Data.Roles);
    }

    [Fact]
    public async Task RegisterCompanyCustomer_DuplicateEmail_ReturnsConflict()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"dup_{uniqueKey}@testcompany.com";
        var password = "Password123";

        var request = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Company {uniqueKey} A",
            ContactPersonName = "John Duplicate",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        // First registration succeeds
        var firstResponse = await client.PostAsJsonAsync("/api/portal/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second registration with same email under same tenant fails with Conflict
        var secondRequest = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Company {uniqueKey} B",
            ContactPersonName = "Jane Duplicate",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        var secondResponse = await client.PostAsJsonAsync("/api/portal/auth/register", secondRequest);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task RegisterCompanyCustomer_PasswordValidation_RejectsEmptyOrMismatch_AcceptsMatchingPassword()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];

        // 1. Mismatched passwords -> 400 Bad Request
        var mismatchReq = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Short Co {uniqueKey}",
            ContactPersonName = "John Short",
            Email = $"mismatch_{uniqueKey}@test.com",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword"
        };

        var responseMismatch = await client.PostAsJsonAsync("/api/portal/auth/register", mismatchReq);
        Assert.Equal(HttpStatusCode.BadRequest, responseMismatch.StatusCode);

        // 2. Empty/whitespace password -> 400 Bad Request
        var emptyReq = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Empty Co {uniqueKey}",
            ContactPersonName = "John Empty",
            Email = $"empty_{uniqueKey}@test.com",
            Password = "",
            ConfirmPassword = ""
        };

        var responseEmpty = await client.PostAsJsonAsync("/api/portal/auth/register", emptyReq);
        Assert.Equal(HttpStatusCode.BadRequest, responseEmpty.StatusCode);

        // 3. Valid non-empty matching password (no 8-char minimum) -> 201 Created
        var validReq = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Valid Pass Co {uniqueKey}",
            ContactPersonName = "John Valid",
            Email = $"valid_{uniqueKey}@test.com",
            Password = "pass1",
            ConfirmPassword = "pass1"
        };

        var responseValid = await client.PostAsJsonAsync("/api/portal/auth/register", validReq);
        Assert.Equal(HttpStatusCode.Created, responseValid.StatusCode);
    }

    [Fact]
    public async Task RegisterCompanyCustomer_MatchingExistingCompany_LinksContactWithoutAutomaticAuthority()
    {
        var client = _fixture.CreateClient();

        // Query existing CustomerAlpha from DB to get exact name and tax number
        string existingCustomerName;
        string? existingTaxNumber;
        Guid existingCustomerId;

        using (var scope = _fixture.Services.CreateScope())
        {
            var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var customer = await domainDb.Customers.FirstOrDefaultAsync(c => c.CustomerId == TestConstants.CustomerAlphaId);
            Assert.NotNull(customer);
            existingCustomerName = customer.CustomerName;
            existingTaxNumber = customer.TaxNumber;
            existingCustomerId = customer.CustomerId;
        }

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"rep_{uniqueKey}@alpha-external.com";
        var password = "SecurePassword123";

        // Register with exact matching company name or tax number
        var request = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = existingCustomerName, // Exact name match
            TaxNumber = existingTaxNumber,
            ContactPersonName = $"Representative {uniqueKey}",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        var response = await client.PostAsJsonAsync("/api/portal/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<CustomerRegistrationResult>>(_jsonOpts);
        Assert.NotNull(content?.Data);

        var result = content.Data;
        Assert.Equal(existingCustomerId, result.CustomerId); // Must link to existing company!
        Assert.True(result.IsExistingCompanyMatched);
        Assert.True(result.RequiresAuthorization); // Authority requires separate validation!
        Assert.True(result.CanLoginImmediately);

        // Verify that no duplicate Customer was created in DomainDbContext
        using (var scope = _fixture.Services.CreateScope())
        {
            var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var customerCount = await domainDb.Customers.CountAsync(c => c.CustomerId == existingCustomerId);
            Assert.Equal(1, customerCount);

            // New contact should be non-primary because existing customer already has a primary contact
            var contact = await domainDb.Contacts.FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant());
            Assert.NotNull(contact);

            var customerContact = await domainDb.CustomerContacts
                .FirstOrDefaultAsync(cc => cc.CustomerId == existingCustomerId && cc.ContactId == contact.ContactId);
            Assert.NotNull(customerContact);
            Assert.False(customerContact.IsPrimary);
        }
    }

    [Fact]
    public async Task RegisterCompanyCustomer_TenantIsolation_SeparateUsersInDifferentTenants()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var email = $"shared_{uniqueKey}@corp.com";
        var password = "SecurePassword123";

        // Register in TenantAlpha
        var reqAlpha = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantAlphaId,
            CompanyName = $"Alpha Co {uniqueKey}",
            ContactPersonName = "User Alpha",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        var resAlpha = await client.PostAsJsonAsync("/api/portal/auth/register", reqAlpha);
        Assert.Equal(HttpStatusCode.Created, resAlpha.StatusCode);

        // Register same email in TenantBeta (different tenant)
        var reqBeta = new RegisterCompanyCustomerRequest
        {
            TenantId = TestConstants.TenantBetaId,
            CompanyName = $"Beta Co {uniqueKey}",
            ContactPersonName = "User Beta",
            Email = email,
            Password = password,
            ConfirmPassword = password
        };

        var resBeta = await client.PostAsJsonAsync("/api/portal/auth/register", reqBeta);
        Assert.Equal(HttpStatusCode.Created, resBeta.StatusCode);
    }
}
