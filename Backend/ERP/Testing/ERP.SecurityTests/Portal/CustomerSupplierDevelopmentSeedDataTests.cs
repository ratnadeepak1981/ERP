using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using API.Features.Portal.DTOs;
using Domain.Features.MasterData.Contact;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using ERP.Domain.Features.MasterData.Supplier;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Core.Models;
using Security.Infrastructure.Persistence;
using Security.Services;
using Xunit;

namespace ERP.SecurityTests.Portal;

public class CustomerSupplierDevelopmentSeedDataTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string TargetTenantIdString = "25ADAB1D-6E1A-4820-A078-DD7AAE6AA7F0";
    private static readonly Guid TargetTenantId = Guid.Parse(TargetTenantIdString);

    public CustomerSupplierDevelopmentSeedDataTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task DevelopmentDatabase_HasThreeMeaningfulSuppliers_WithExactPromptEmails()
    {
        // Query the live development DomainDB (read-only verification)
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DomainDB;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        await using var db = new DomainDbContext(options);

        var suppliers = await db.Suppliers
            .Where(s => s.TenantId == TargetTenantId)
            .OrderBy(s => s.SupplierCode)
            .ToListAsync();

        Assert.Equal(3, suppliers.Count);

        var s1 = suppliers.FirstOrDefault(s => s.Email == "ratnadeepak1981@gmail.com");
        Assert.NotNull(s1);
        Assert.Equal("Lanka Retail Distributors (Pvt) Ltd", s1.SupplierName);
        Assert.Equal("SUP-RET-001", s1.SupplierCode);

        var s2 = suppliers.FirstOrDefault(s => s.Email == "pathmanathanratnadeepak@gmail.com");
        Assert.NotNull(s2);
        Assert.Equal("Prime Ceylon Provisions (Pvt) Ltd", s2.SupplierName);
        Assert.Equal("SUP-RET-002", s2.SupplierCode);

        var s3 = suppliers.FirstOrDefault(s => s.Email == "ratnadeepak@yahoo.com");
        Assert.NotNull(s3);
        Assert.Equal("Apex Wholesale Importers PLC", s3.SupplierName);
        Assert.Equal("SUP-RET-003", s3.SupplierCode);

        // Verify contacts and link
        var supplierIds = suppliers.Select(s => s.SupplierId).ToList();
        var links = await db.SupplierContacts
            .Where(sc => sc.TenantId == TargetTenantId && supplierIds.Contains(sc.SupplierId) && sc.IsActive)
            .ToListAsync();

        Assert.Equal(3, links.Count);
        Assert.All(links, l => Assert.True(l.IsPrimary));

        var contactIds = links.Select(l => l.ContactId).ToList();
        var contacts = await db.Contacts
            .Where(c => c.TenantId == TargetTenantId && contactIds.Contains(c.ContactId) && c.IsActive)
            .ToListAsync();

        Assert.Equal(3, contacts.Count);
        Assert.Contains(contacts, c => c.Email == "ratnadeepak1981@gmail.com");
        Assert.Contains(contacts, c => c.Email == "pathmanathanratnadeepak@gmail.com");
        Assert.Contains(contacts, c => c.Email == "ratnadeepak@yahoo.com");
    }

    [Fact]
    public async Task DevelopmentDatabase_HasThreeMeaningfulCustomers_WithExactNotificationEmails()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=DomainDB;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        await using var db = new DomainDbContext(options);

        var customers = await db.Customers
            .Where(c => c.TenantId == TargetTenantId)
            .OrderBy(c => c.CustomerCode)
            .ToListAsync();

        Assert.Equal(3, customers.Count);

        var c1 = customers.FirstOrDefault(c => c.Email == "ratnadeepak1981@gmail.com1");
        Assert.NotNull(c1);
        Assert.Equal("Cargills Supermarkets PLC", c1.CustomerName);
        Assert.Equal("CUST-RET-001", c1.CustomerCode);

        var c2 = customers.FirstOrDefault(c => c.Email == "pathmanathanratnadeepak@gmail.com1");
        Assert.NotNull(c2);
        Assert.Equal("Keells Food Products PLC", c2.CustomerName);
        Assert.Equal("CUST-RET-002", c2.CustomerCode);

        var c3 = customers.FirstOrDefault(c => c.Email == "ratnadeepak@yahoo.com1");
        Assert.NotNull(c3);
        Assert.Equal("Ceylon Beverage Holdings PLC", c3.CustomerName);
        Assert.Equal("CUST-RET-003", c3.CustomerCode);

        // Verify customer contacts and primary links
        var customerIds = customers.Select(c => c.CustomerId).ToList();
        var links = await db.CustomerContacts
            .Where(cc => cc.TenantId == TargetTenantId && customerIds.Contains(cc.CustomerId) && cc.IsActive)
            .ToListAsync();

        Assert.Equal(3, links.Count);
        Assert.All(links, l => Assert.True(l.IsPrimary));

        var contactIds = links.Select(l => l.ContactId).ToList();
        var contacts = await db.Contacts
            .Where(c => c.TenantId == TargetTenantId && contactIds.Contains(c.ContactId) && c.IsActive)
            .ToListAsync();

        Assert.Equal(3, contacts.Count);
        Assert.Contains(contacts, c => c.Email == "ratnadeepak1981@gmail.com1");
        Assert.Contains(contacts, c => c.Email == "pathmanathanratnadeepak@gmail.com1");
        Assert.Contains(contacts, c => c.Email == "ratnadeepak@yahoo.com1");
    }

    [Fact]
    public async Task DevelopmentDatabase_HasThreeCustomerUserAccounts_WithPassword123_AndSeparateLoginEmails()
    {
        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SecurityDB;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        await using var securityDb = new SecurityDbContext(options);
        var passwordService = new PasswordService();

        var customerUsers = await securityDb.Users
            .Where(u => u.TenantId == TargetTenantId && u.Username.Contains(".portal@"))
            .ToListAsync();

        Assert.Equal(3, customerUsers.Count);

        var u1 = customerUsers.FirstOrDefault(u => u.Username == "cargills.portal@cargillsretail.lk");
        Assert.NotNull(u1);
        Assert.Equal("cargills.portal@cargillsretail.lk", u1.Email);
        Assert.True(passwordService.VerifyPassword("123", u1.PasswordHash));

        var u2 = customerUsers.FirstOrDefault(u => u.Username == "keells.portal@keells.lk");
        Assert.NotNull(u2);
        Assert.Equal("keells.portal@keells.lk", u2.Email);
        Assert.True(passwordService.VerifyPassword("123", u2.PasswordHash));

        var u3 = customerUsers.FirstOrDefault(u => u.Username == "ceylonbev.portal@ceylonbev.lk");
        Assert.NotNull(u3);
        Assert.Equal("ceylonbev.portal@ceylonbev.lk", u3.Email);
        Assert.True(passwordService.VerifyPassword("123", u3.PasswordHash));

        // Verify roles
        var userIds = customerUsers.Select(u => u.Id).ToList();
        var userRoles = await securityDb.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync();

        Assert.Equal(3, userRoles.Count);
        Assert.All(userRoles, ur => Assert.Equal("CUSTOMER_COMPANY_ADMIN", ur.Role.Name));
    }

    [Fact]
    public async Task CustomerLogin_WithPassword123_AuthenticatesSuccessfully_AndResolvesCustomerAndContact()
    {
        var client = _fixture.CreateClient();
        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var notificationEmail = $"cust_{uniqueKey}@testnotif.com1";
        var loginEmail = $"login_{uniqueKey}@cargillsretail.lk";
        var password = "123";

        // 1. Seed customer, contact, and security user in test database
        using (var scope = _fixture.Services.CreateScope())
        {
            var domainDb = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var securityDb = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
            var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();

            var customer = Customer.Create(
                TestConstants.TenantAlphaId,
                $"CUST-{uniqueKey}",
                $"Test Customer {uniqueKey} PLC",
                contactPerson: "Jane Buyer",
                email: notificationEmail);
            domainDb.Customers.Add(customer);

            var userId = Guid.NewGuid();
            var contact = Contact.Create(
                TestConstants.TenantAlphaId,
                "Jane Buyer",
                "Procurement Lead",
                notificationEmail,
                contactId: userId);
            domainDb.Contacts.Add(contact);

            var contactType = await domainDb.ContactTypes.FirstAsync(ct => ct.Code == "GENERAL" || ct.TenantId == null);
            var customerContact = CustomerContact.Create(
                TestConstants.TenantAlphaId,
                customer.CustomerId,
                contact.ContactId,
                contactType.ContactTypeId,
                isPrimary: true);
            domainDb.CustomerContacts.Add(customerContact);

            await domainDb.SaveChangesAsync();

            var user = new User
            {
                Id = userId,
                TenantId = TestConstants.TenantAlphaId,
                Username = loginEmail,
                Email = loginEmail,
                PasswordHash = passwordService.HashPassword(password),
                IsActive = true
            };
            securityDb.Users.Add(user);

            var role = await securityDb.Roles.FirstOrDefaultAsync(r => r.TenantId == TestConstants.TenantAlphaId && r.Name == "CUSTOMER_COMPANY_ADMIN")
                       ?? new Role
                       {
                           Id = Guid.NewGuid(),
                           TenantId = TestConstants.TenantAlphaId,
                           Name = "CUSTOMER_COMPANY_ADMIN",
                           Description = "Customer Admin",
                           IsActive = true,
                           TemplateKey = "CUSTOMER_COMPANY_ADMIN",
                           TemplateVersion = 1
                       };

            if (!securityDb.Roles.Local.Contains(role) && !await securityDb.Roles.AnyAsync(r => r.Id == role.Id))
            {
                securityDb.Roles.Add(role);
            }

            securityDb.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = role.Id
            });

            await securityDb.SaveChangesAsync();
        }

        // 2. Perform Login with login email and password 123
        var loginRequest = new CustomerLoginRequest
        {
            Email = loginEmail,
            Password = password
        };

        var loginResponse = await client.PostAsJsonAsync("/api/portal/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerLoginResponse>>(_jsonOpts);
        Assert.NotNull(loginResult?.Data);

        var data = loginResult.Data;
        Assert.Equal("CUSTOMER", data.PortalType);
        Assert.Equal(TestConstants.TenantAlphaId, data.TenantId);
        Assert.Equal(loginEmail, data.Email);
        Assert.Contains("CUSTOMER_COMPANY_ADMIN", data.Roles);
        Assert.Contains("PORTAL_PROFILE.VIEW", data.Permissions);

        // 3. Inspect JWT Token Claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(data.AccessToken);
        Assert.Equal("CUSTOMER", jwt.Claims.First(c => c.Type == "portal_type").Value);
        Assert.Equal(TestConstants.TenantAlphaId.ToString(), jwt.Claims.First(c => c.Type == "tenant_id").Value);

        // 4. Access Customer Profile
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.AccessToken);
        var profileResponse = await client.GetAsync("/api/portal/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profileResult = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerProfileDto>>(_jsonOpts);
        Assert.NotNull(profileResult?.Data);
        Assert.Equal(notificationEmail, profileResult.Data.Contact.Email);
        Assert.Equal(notificationEmail, profileResult.Data.Customer.Email);

        // 5. Customer JWT MUST be forbidden from employee-only endpoints
        var employeeResp = await client.GetAsync($"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-orders");
        Assert.Equal(HttpStatusCode.Forbidden, employeeResp.StatusCode);

        var tenantResp = await client.GetAsync("/api/Tenant");
        Assert.Equal(HttpStatusCode.Forbidden, tenantResp.StatusCode);
    }
}
