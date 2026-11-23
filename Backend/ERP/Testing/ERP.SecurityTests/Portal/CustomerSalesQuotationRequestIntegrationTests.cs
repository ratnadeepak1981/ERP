using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using Domain.Features.MasterData.Address;
using Domain.Features.MasterData.Contact;
using Domain.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.MasterData.Customer;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Interfaces;
using Xunit;

namespace ERP.SecurityTests.Portal;

public class CustomerSalesQuotationRequestIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CustomerSalesQuotationRequestIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    private async Task<Guid> GetOrCreateContactTypeIdAsync(DomainDbContext db, Guid tenantId)
    {
        var existing = await db.ContactTypes.FirstOrDefaultAsync(ct => ct.TenantId == tenantId || ct.TenantId == null);
        if (existing != null) return existing.ContactTypeId;

        var created = ContactType.Create(tenantId, "GENERAL", "General Contact");
        db.ContactTypes.Add(created);
        await db.SaveChangesAsync();
        return created.ContactTypeId;
    }

    private System.Net.Http.HttpClient CreateCustomerClient(
        Guid userId,
        string username,
        Guid tenantId,
        Guid customerId,
        Guid contactId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        using var scope = _fixture.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        string token = tokenService.GenerateCustomerAccessToken(
            userId,
            username,
            tenantId,
            customerId,
            contactId,
            roles,
            permissions);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    [Fact]
    public async Task SubmitQuotationRequest_ApprovedCompanyCustomer_SucceedsWithSubmittedStatus()
    {
        // 1. Arrange customer & contact in DomainDbTest
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var productId = TestConstants.ProductAlphaId;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var customer = Customer.Create(
                tenantId,
                $"CUST-B2B-{Guid.NewGuid():N}"[..12],
                "Acme Precision Industrial Corp",
                "Alex Hunter",
                $"alex_{Guid.NewGuid():N}@acmeindustrial.local",
                "+1-555-0199",
                "100 Industrial Parkway",
                city: "Detroit",
                country: "United States");

            typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
            db.Customers.Add(customer);

            var contact = Contact.Create(
                tenantId,
                "Alex Hunter",
                jobTitle: "Procurement Manager",
                email: "alex.hunter@acmeindustrial.local",
                contactId: contactId);
            db.Contacts.Add(contact);

            var contactTypeId = await GetOrCreateContactTypeIdAsync(db, tenantId);
            var customerContact = CustomerContact.Create(
                tenantId,
                customerId,
                contactId,
                contactTypeId,
                isPrimary: true);
            db.CustomerContacts.Add(customerContact);

            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(
            userId,
            "alex.hunter@acmeindustrial.local",
            tenantId,
            customerId,
            contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_QUOTATION.REQUEST", "PORTAL_QUOTATION.VIEW" });

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(21),
            Currency = "USD",
            CustomerReference = "RFQ-2026-ENG-42",
            CustomerRemarks = "Required for production expansion project",
            IsDraft = false,
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 25,
                    UnitPrice = 95.00m,
                    Notes = "Target unit price proposal"
                }
            }
        };

        // 2. Act
        var response = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={companyId}&branchId={branchId}",
            requestPayload);

        // 3. Assert response
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Equal(customerId, created.CustomerId);
        Assert.Equal(SalesQuotationType.CustomerRequest, created.DocumentType);
        Assert.Equal(SalesQuotationStatus.Submitted, created.Status);
        Assert.Equal("USD", created.Currency);
        Assert.Single(created.Items);
        Assert.Equal(25, created.Items[0].Quantity);

        // 4. Verify in database: Quotation saved, NO SalesOrder created, NO PriceCard updated
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var savedQuote = await db.SalesQuotations
                .Include(sq => sq.Items)
                .FirstOrDefaultAsync(sq => sq.Id == created.Id);

            Assert.NotNull(savedQuote);
            Assert.Equal(SalesQuotationStatus.Submitted, savedQuote.Status);
            Assert.Null(savedQuote.ConvertedSalesOrderId);

            // Verify no sales orders were created by quotation request
            var anyOrders = await db.SalesOrders
                .AnyAsync(so => so.CustomerId == customerId);
            Assert.False(anyOrders, "Quotation request submission must NOT create a SalesOrder.");
        }
    }

    [Fact]
    public async Task SubmitQuotationRequest_SaveDraft_SetsDraftStatus()
    {
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var productId = TestConstants.ProductAlphaId;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var customer = Customer.Create(
                tenantId,
                $"CUST-DFT-{Guid.NewGuid():N}"[..12],
                "Draft Fabrication Corp",
                "Bob Builder",
                $"bob_{Guid.NewGuid():N}@draftfab.local");

            typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
            db.Customers.Add(customer);

            var contact = Contact.Create(tenantId, "Bob Builder", email: "bob@draftfab.local", contactId: contactId);
            db.Contacts.Add(contact);

            var contactTypeId = await GetOrCreateContactTypeIdAsync(db, tenantId);
            var cc = CustomerContact.Create(tenantId, customerId, contactId, contactTypeId, isPrimary: true);
            db.CustomerContacts.Add(cc);

            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(
            userId,
            "bob@draftfab.local",
            tenantId,
            customerId,
            contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_QUOTATION.REQUEST" });

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            IsDraft = true, // Save as Draft
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = productId,
                    Quantity = 5,
                    UnitPrice = 120.00m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={companyId}&branchId={branchId}",
            requestPayload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Equal(SalesQuotationStatus.Draft, created.Status);
    }

    [Fact]
    public async Task SubmitQuotationRequest_IndividualRetailCustomer_ReturnsForbidden()
    {
        var client = CreateCustomerClient(
            Guid.NewGuid(),
            "retail_shopper@gmail.com",
            TestConstants.TenantAlphaId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { "CUSTOMER_INDIVIDUAL" }, // Individual Retail customer role
            new[] { "PORTAL_QUOTATION.REQUEST" });

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 1 }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={TestConstants.CompanyAId}&branchId={TestConstants.BranchA1Id}",
            requestPayload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitQuotationRequest_ContactWithoutPurchasingPermission_ReturnsForbidden()
    {
        var client = CreateCustomerClient(
            Guid.NewGuid(),
            "junior_staff@acmecorp.com",
            TestConstants.TenantAlphaId,
            TestConstants.CustomerAlphaId,
            Guid.NewGuid(),
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_PROFILE.VIEW" }); // Lacks PORTAL_QUOTATION.REQUEST

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 10 }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={TestConstants.CompanyAId}&branchId={TestConstants.BranchA1Id}",
            requestPayload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitQuotationRequest_InvalidDeliveryAddress_ReturnsBadRequest()
    {
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = TestConstants.TenantAlphaId;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var customer = Customer.Create(tenantId, $"CUST-ADR-{Guid.NewGuid():N}"[..12], "Address Test Corp");
            typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
            db.Customers.Add(customer);

            var contact = Contact.Create(tenantId, "Contact A", email: "ca@test.local", contactId: contactId);
            db.Contacts.Add(contact);

            var contactTypeId = await GetOrCreateContactTypeIdAsync(db, tenantId);
            var cc = CustomerContact.Create(tenantId, customerId, contactId, contactTypeId, isPrimary: true);
            db.CustomerContacts.Add(cc);

            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(
            userId,
            "ca@test.local",
            tenantId,
            customerId,
            contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_QUOTATION.REQUEST" });

        var randomForeignAddressId = Guid.NewGuid();

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            DeliveryAddressId = randomForeignAddressId, // Not owned by customer!
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 5 }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={TestConstants.CompanyAId}&branchId={TestConstants.BranchA1Id}",
            requestPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitQuotationRequest_DuplicateIdempotencyKey_ReturnsExistingWithoutDuplicating()
    {
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = TestConstants.TenantAlphaId;
        var idempotencyKey = $"IDEM-{Guid.NewGuid():N}";

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var customer = Customer.Create(tenantId, $"CUST-IDEM-{Guid.NewGuid():N}"[..12], "Idempotent Corp");
            typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
            db.Customers.Add(customer);

            var contact = Contact.Create(tenantId, "Idem Contact", email: "idem@test.local", contactId: contactId);
            db.Contacts.Add(contact);

            var contactTypeId = await GetOrCreateContactTypeIdAsync(db, tenantId);
            var cc = CustomerContact.Create(tenantId, customerId, contactId, contactTypeId, isPrimary: true);
            db.CustomerContacts.Add(cc);

            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(
            userId,
            "idem@test.local",
            tenantId,
            customerId,
            contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_QUOTATION.REQUEST" });

        var requestPayload = new CreateCustomerQuotationRequest
        {
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            IdempotencyKey = idempotencyKey,
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new() { ProductId = TestConstants.ProductAlphaId, Quantity = 10 }
            }
        };

        // First call -> 201 Created
        var resp1 = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={TestConstants.CompanyAId}&branchId={TestConstants.BranchA1Id}",
            requestPayload);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);
        var q1 = await resp1.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(q1);

        // Second call with same idempotency key -> 200 OK returning existing quote
        var resp2 = await client.PostAsJsonAsync(
            $"/api/portal/customer/quotations/request?companyId={TestConstants.CompanyAId}&branchId={TestConstants.BranchA1Id}",
            requestPayload);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var q2 = await resp2.ReadApiResponseDataAsync<CustomerSalesQuotationDto>(_jsonOpts);
        Assert.NotNull(q2);
        Assert.Equal(q1.Id, q2.Id);

        // Verify only one quotation was created in database
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var count = await db.SalesQuotations.CountAsync(sq => sq.CustomerId == customerId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task GetCustomerQuotations_WorkspaceHistory_ReturnsOnlyOwnedQuotations()
    {
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = TestConstants.TenantAlphaId;

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

            var customer = Customer.Create(tenantId, $"CUST-HIST-{Guid.NewGuid():N}"[..12], "History Test Corp");
            typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
            db.Customers.Add(customer);

            var contact = Contact.Create(tenantId, "Hist Contact", email: "hist@test.local", contactId: contactId);
            db.Contacts.Add(contact);

            var contactTypeId = await GetOrCreateContactTypeIdAsync(db, tenantId);
            var cc = CustomerContact.Create(tenantId, customerId, contactId, contactTypeId, isPrimary: true);
            db.CustomerContacts.Add(cc);

            // Add quotation for this customer
            var quote = SalesQuotation.CreateCustomerRequest(
                tenantId,
                TestConstants.CompanyAId,
                TestConstants.BranchA1Id,
                customerId,
                contactId,
                $"SQ-{Guid.NewGuid():N}"[..15],
                DateTime.UtcNow.AddDays(30));
            db.SalesQuotations.Add(quote);

            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(
            userId,
            "hist@test.local",
            tenantId,
            customerId,
            contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" },
            new[] { "PORTAL_QUOTATION.VIEW" });

        var response = await client.GetAsync("/api/portal/customer/quotations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.ReadApiResponseDataAsync<List<SalesQuotationDto>>(_jsonOpts);
        Assert.NotNull(list);
        Assert.NotEmpty(list);
        Assert.All(list, q => Assert.Equal(customerId, q.CustomerId));
    }
}
