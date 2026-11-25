using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using API.Contracts;
using ERP.Domain.Features.MasterData.Category;
using Domain.Features.MasterData.Contact;
using ERP.Domain.Features.MasterData.Customer;
using Domain.Features.MasterData.PriceCard;
using Domain.Features.MasterData.Product;
using Domain.Features.Sales.SalesQuotation;
using ERP.Application.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Security.Interfaces;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesQuotationAcceptanceAndPriceCardTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesQuotationAcceptanceAndPriceCardTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
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

    private System.Net.Http.HttpClient CreateEmployeeClient(
        Guid userId,
        string username,
        Guid tenantId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        using var scope = _fixture.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        string token = tokenService.GenerateAccessToken(
            userId,
            username,
            tenantId,
            roles,
            permissions);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private async Task<(Guid customerId, Guid contactId)> SeedCustomerAndContactAsync(
        DomainDbContext db,
        Guid tenantId,
        string customerCode,
        string customerName)
    {
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var uniqueCode = $"{customerCode}_{Guid.NewGuid():N}"[..25];
        var uniqueName = $"{customerName}_{Guid.NewGuid():N}"[..30];
        var customer = Customer.Create(
            tenantId,
            uniqueCode,
            uniqueName,
            "Contact Person",
            $"{uniqueCode.ToLowerInvariant()}@example.local",
            "+1-555-0100",
            "100 Enterprise Way",
            city: "Industrial Park",
            country: "United States");

        typeof(Customer).GetProperty("CustomerId")!.SetValue(customer, customerId);
        db.Customers.Add(customer);

        var contact = Contact.Create(
            tenantId,
            "Jane Doe",
            jobTitle: "Procurement Lead",
            email: "jane@example.com",
            contactId: contactId);
        db.Contacts.Add(contact);

        var contactType = await db.ContactTypes.FirstOrDefaultAsync(ct => ct.TenantId == tenantId || ct.TenantId == null);
        var contactTypeId = contactType?.ContactTypeId ?? Guid.NewGuid();
        if (contactType == null)
        {
            var created = ContactType.Create(tenantId, "GEN", "General Contact");
            db.ContactTypes.Add(created);
            contactTypeId = created.ContactTypeId;
        }

        var customerContact = CustomerContact.Create(
            tenantId,
            customerId,
            contactId,
            contactTypeId,
            isPrimary: true);
        db.CustomerContacts.Add(customerContact);

        await db.SaveChangesAsync();
        return (customerId, contactId);
    }

    private async Task<Guid> SeedProductAsync(
        DomainDbContext db,
        Guid tenantId,
        string productCode,
        string productName,
        decimal sellingPrice)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (category == null)
        {
            category = Category.Create(tenantId, $"CAT_{Guid.NewGuid():N}"[..18], "General Category");
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var uniqueCode = $"{productCode}_{Guid.NewGuid():N}"[..25];
        var uniqueName = $"{productName}_{Guid.NewGuid():N}"[..30];
        var product = Product.Create(
            tenantId,
            uniqueCode,
            uniqueName,
            category.CategoryId,
            sellingPrice: sellingPrice,
            isSellable: true);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.ProductId;
    }

    [Fact]
    public async Task CustomerAcceptance_WhenFormallyIssued_RecordsAcceptanceOnly_AndDoesNotCreateSalesOrder()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-ACCEPT-1", "Alpha Global Industries");
        var productId = await SeedProductAsync(db, tenantId, "PROD-ACCEPT-1", "Precision Sensor A", 250m);

        // 1. Arrange a formal commercial quotation in Sent status
        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-ACC-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            notes: "Commercial proposal for sensors",
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 20m, 220m, discountRate: 5m, taxRate: 10m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();

        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        Assert.Equal(SalesQuotationStatus.Sent, quotation.Status);

        // 2. Customer reviews and accepts quotation via Customer Portal API
        var customerClient = CreateCustomerClient(
            customerUserId,
            "jane@example.com",
            tenantId,
            customerId,
            contactId,
            roles: new[] { "CUSTOMER_COMPANY_ADMIN" },
            permissions: new[] { "PORTAL_QUOTATION.ACCEPT" });

        var acceptReq = new { customerRemarks = "Accepted per negotiated commercial terms." };
        var response = await customerClient.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", acceptReq);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3. Verify Database: Quotation is Accepted ONLY, NO SalesOrder created, NO ConvertedSalesOrderId
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var updatedQuote = await verifyDb.SalesQuotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotation.Id);

        Assert.NotNull(updatedQuote);
        Assert.Equal(SalesQuotationStatus.Accepted, updatedQuote.Status);
        Assert.NotNull(updatedQuote.AcceptedAtUtc);
        Assert.Equal(contactId, updatedQuote.CustomerContactId);
        Assert.Null(updatedQuote.ConvertedSalesOrderId);

        // Crucial Architectural Rule: Customer acceptance MUST NOT generate a Sales Order!
        var ordersCount = await verifyDb.SalesOrders.CountAsync(o => o.CustomerId == customerId);
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task InternalConversion_AfterCustomerAcceptance_CreatesSingleDraftSalesOrder_AndAutoGeneratesPriceCard()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-CONV-1", "Alpha Global Industries 2");
        var productId = await SeedProductAsync(db, tenantId, "PROD-CONV-1", "Precision Sensor B", 300m);

        // 1. Arrange a quotation accepted by customer
        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-CNV-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            notes: "Commercial proposal for sensors B",
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 20m, 280m, discountRate: 5m, taxRate: 10m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        quotation.Accept(contactId);

        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        Assert.Equal(SalesQuotationStatus.Accepted, quotation.Status);

        // 2. Authorised ERP employee explicitly converts the accepted quotation to Sales Order
        var employeeClient = CreateEmployeeClient(
            employeeUserId,
            "sales.officer@apexerp.com",
            tenantId,
            roles: new[] { "SALES_OFFICER" },
            permissions: new[] { "SALES_ORDER.CREATE", "SALES_QUOTATION.VIEW" });

        var response = await employeeClient.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-quotations/{quotation.Id}/convert-to-order",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 3. Verify Database: Quotation is now Converted, exactly ONE Draft SalesOrder exists
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var updatedQuote = await verifyDb.SalesQuotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotation.Id);

        Assert.NotNull(updatedQuote);
        Assert.Equal(SalesQuotationStatus.Converted, updatedQuote.Status);
        Assert.NotNull(updatedQuote.ConvertedSalesOrderId);

        // Verify Draft Sales Order
        var order = await verifyDb.SalesOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == updatedQuote.ConvertedSalesOrderId!.Value);

        Assert.NotNull(order);
        Assert.Equal("Draft", order.Status.ToString());
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(companyId, order.CompanyId);
        Assert.Equal(branchId, order.BranchId);
        Assert.Single(order.Items);
        Assert.Equal(productId, order.Items.First().ProductId);
        Assert.Equal(20m, order.Items.First().Quantity);
        Assert.Equal(280m, order.Items.First().UnitPrice);

        // Verify Price Card creation: Missing price card auto-created and assigned
        var customer = await verifyDb.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        Assert.NotNull(customer);
        Assert.NotNull(customer.DefaultSalesPriceCardId);

        var priceCard = await verifyDb.PriceCards
            .Include(pc => pc.Lines)
            .FirstOrDefaultAsync(pc => pc.PriceCardId == customer.DefaultSalesPriceCardId!.Value);

        Assert.NotNull(priceCard);
        Assert.Equal(PriceCardType.Sales, priceCard.PriceCardType);
        Assert.Equal("USD", priceCard.Currency);
        Assert.True(priceCard.IsActive);
        Assert.Single(priceCard.Lines);
        Assert.Equal(productId, priceCard.Lines.First().ProductId);
        Assert.Equal(280m, priceCard.Lines.First().UnitPrice);
    }

    [Fact]
    public async Task CustomerAcceptance_WhenAlreadyAssignedPriceCard_UpdatesEntriesNonDestructively()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-ACCEPT-2", "Beta Tech Solutions");
        var productId = await SeedProductAsync(db, tenantId, "PROD-ACCEPT-2", "Industrial Router X", 500m);

        // Seed an existing active Price Card for this customer
        var existingCard = PriceCard.Create(
            tenantId,
            $"SPC-CUST2-{Guid.NewGuid():N}"[..20],
            "Beta Tech Sales Price Card",
            PriceCardType.Sales,
            "USD",
            effectiveFrom: DateTime.UtcNow.AddMonths(-1));

        existingCard.AddLine(productId, unitPrice: 480m, minimumQuantity: 5m, effectiveFrom: DateTime.UtcNow.AddMonths(-1));
        db.PriceCards.Add(existingCard);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        customer!.SetDefaultSalesPriceCard(existingCard.PriceCardId);

        // Arrange formal quotation with newly negotiated price of 450m
        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-ACC2-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(10),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 10m, 450m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        // Customer accepts
        var customerClient = CreateCustomerClient(
            customerUserId,
            "jane@example.com",
            tenantId,
            customerId,
            contactId,
            roles: new[] { "CUSTOMER_COMPANY_ADMIN" },
            permissions: new[] { "PORTAL_QUOTATION.ACCEPT" });

        var response = await customerClient.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Employee converts accepted quotation to Sales Order (triggering Price Card update)
        var employeeClient = CreateEmployeeClient(
            employeeUserId,
            "sales.officer@apexerp.com",
            tenantId,
            roles: new[] { "SALES_OFFICER" },
            permissions: new[] { "SALES_ORDER.CREATE", "SALES_QUOTATION.VIEW" });

        var convertResp = await employeeClient.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-quotations/{quotation.Id}/convert-to-order",
            null);
        Assert.Equal(HttpStatusCode.OK, convertResp.StatusCode);

        // Verify that existing card was updated with negotiated commercial terms
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var updatedCard = await verifyDb.PriceCards
            .Include(pc => pc.Lines)
            .FirstOrDefaultAsync(pc => pc.PriceCardId == existingCard.PriceCardId);

        Assert.NotNull(updatedCard);
        var activeLine = updatedCard.Lines.FirstOrDefault(l => l.ProductId == productId);
        Assert.NotNull(activeLine);
        Assert.Equal(450m, activeLine.UnitPrice);
        Assert.Equal(10m, activeLine.MinimumQuantity);
    }

    [Fact]
    public async Task InternalConversion_WithoutVerifiedCustomerAcceptance_IsStrictlyRejected()
    {
        // Enforce: "No verified customer acceptance = No Sales Order."
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;

        var (customerId, _) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-GATE-1", "Gamma Enterprise");
        var productId = await SeedProductAsync(db, tenantId, "PROD-GATE-1", "Fiber Cable 100m", 80m);

        // Quotation in Sent status (sent to customer, but NOT yet accepted by customer)
        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-GATE-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 5m, 75m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        Assert.Equal(SalesQuotationStatus.Sent, quotation.Status);

        // Employee attempts to convert via internal API endpoint before customer accepts
        var employeeClient = CreateEmployeeClient(
            employeeUserId,
            "sales.officer@apexerp.com",
            tenantId,
            roles: new[] { "SALES_OFFICER" },
            permissions: new[] { "SALES_ORDER.CREATE", "SALES_QUOTATION.VIEW" });

        var response = await employeeClient.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-quotations/{quotation.Id}/convert-to-order",
            null);

        // Must be rejected because quotation has not been accepted by customer
        Assert.False(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("customer acceptance", content, StringComparison.OrdinalIgnoreCase);

        // Assert no sales order was created
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var orderExists = await verifyDb.SalesOrders.AnyAsync(o => o.CustomerId == customerId);
        Assert.False(orderExists);
    }

    [Fact]
    public async Task CustomerDecline_WithMandatoryRemarks_TransitionsToDeclinedByCustomer()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-DEC-1", "Delta Corp");
        var productId = await SeedProductAsync(db, tenantId, "PROD-DEC-1", "Steel Plate", 150m);

        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-DEC-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 10m, 140m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        var customerClient = CreateCustomerClient(
            customerUserId,
            "jane@example.com",
            tenantId,
            customerId,
            contactId,
            roles: new[] { "CUSTOMER_COMPANY_ADMIN" },
            permissions: new[] { "PORTAL_QUOTATION.ACCEPT" });

        var declineReq = new { remarks = "Budget cut; postponing order to next quarter." };
        var response = await customerClient.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/decline", declineReq);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var declinedQuote = await verifyDb.SalesQuotations.FirstOrDefaultAsync(q => q.Id == quotation.Id);

        Assert.NotNull(declinedQuote);
        Assert.Equal(SalesQuotationStatus.DeclinedByCustomer, declinedQuote.Status);
        Assert.Equal("Budget cut; postponing order to next quarter.", declinedQuote.DeclinedRemarks);
        Assert.NotNull(declinedQuote.DeclinedAtUtc);
    }

    [Fact]
    public async Task CustomerAcceptance_ExpiredQuotation_IsRejected()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-EXP-1", "Epsilon Systems");
        var productId = await SeedProductAsync(db, tenantId, "PROD-EXP-1", "Router 5G", 300m);

        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-EXP-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(-1), // Already expired
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 5m, 290m);
        // Force valid until into the past
        typeof(SalesQuotation).GetProperty(nameof(SalesQuotation.ValidUntilDateUtc))!
            .SetValue(quotation, DateTime.UtcNow.AddDays(-1));
        typeof(SalesQuotation).GetProperty(nameof(SalesQuotation.Status))!
            .SetValue(quotation, SalesQuotationStatus.Sent);

        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        var customerClient = CreateCustomerClient(
            customerUserId,
            "jane@example.com",
            tenantId,
            customerId,
            contactId,
            roles: new[] { "CUSTOMER_COMPANY_ADMIN" },
            permissions: new[] { "PORTAL_QUOTATION.ACCEPT" });

        var response = await customerClient.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", new { });

        Assert.False(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("expired", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerAcceptance_ConcurrentRequests_Idempotent_TransitionsToAcceptedOnly()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-CONCUR-1", "Zeta Concurrency Corp");
        var productId = await SeedProductAsync(db, tenantId, "PROD-CONCUR-1", "High-Torque Motor", 600m);

        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-CON-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 10m, 550m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        var customerClient1 = CreateCustomerClient(
            customerUserId, "jane@example.com", tenantId, customerId, contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" }, new[] { "PORTAL_QUOTATION.ACCEPT" });

        var customerClient2 = CreateCustomerClient(
            customerUserId, "jane@example.com", tenantId, customerId, contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" }, new[] { "PORTAL_QUOTATION.ACCEPT" });

        // Execute concurrent acceptance calls
        var task1 = customerClient1.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", new { });
        var task2 = customerClient2.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", new { });

        var responses = await Task.WhenAll(task1, task2);

        // At least one should succeed with 200 OK
        Assert.All(responses, r => Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);

        // Crucial: Acceptance MUST NOT create any Sales Order!
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var updatedQuote = await verifyDb.SalesQuotations.FirstOrDefaultAsync(q => q.Id == quotation.Id);
        Assert.NotNull(updatedQuote);
        Assert.Equal(SalesQuotationStatus.Accepted, updatedQuote.Status);

        var orders = await verifyDb.SalesOrders.Where(o => o.CustomerId == customerId).ToListAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CustomerAcceptance_WhenSharedPriceCardExists_DoesNotMutateSharedCard_CreatesCustomerSpecificCard()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-ISOLATE-1", "Theta Multi-Tenant Ltd");
        var productId = await SeedProductAsync(db, tenantId, "PROD-ISOLATE-1", "Shared Bearing X", 100m);

        // Seed a generic/shared active Sales Price Card in USD
        var sharedCard = PriceCard.Create(
            tenantId,
            $"SPC-GEN-{Guid.NewGuid():N}"[..20],
            "General Wholesale USD Price Card",
            PriceCardType.Sales,
            "USD",
            effectiveFrom: DateTime.UtcNow.AddMonths(-2));

        sharedCard.AddLine(productId, unitPrice: 95m, minimumQuantity: 1m, effectiveFrom: DateTime.UtcNow.AddMonths(-2));
        db.PriceCards.Add(sharedCard);
        await db.SaveChangesAsync();

        // Customer has no default price card
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        Assert.Null(customer!.DefaultSalesPriceCardId);

        // Customer receives formal quotation with negotiated price: 80m
        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-ISO-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 50m, 80m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        // Customer accepts quotation
        var customerClient = CreateCustomerClient(
            customerUserId, "jane@example.com", tenantId, customerId, contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" }, new[] { "PORTAL_QUOTATION.ACCEPT" });

        var response = await customerClient.PostAsJsonAsync($"/api/portal/customer/quotations/{quotation.Id}/accept", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Employee converts accepted quotation to Sales Order (triggering Price Card resolution and customer-specific card creation)
        var employeeClient = CreateEmployeeClient(
            employeeUserId,
            "sales.officer@apexerp.com",
            tenantId,
            roles: new[] { "SALES_OFFICER" },
            permissions: new[] { "SALES_ORDER.CREATE", "SALES_QUOTATION.VIEW" });

        var convertResp = await employeeClient.PostAsync(
            $"/api/companies/{companyId}/branches/{branchId}/sales-quotations/{quotation.Id}/convert-to-order",
            null);
        Assert.Equal(HttpStatusCode.OK, convertResp.StatusCode);

        // Verify that the shared card was NOT modified
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var unmutatedSharedCard = await verifyDb.PriceCards
            .Include(pc => pc.Lines)
            .FirstOrDefaultAsync(pc => pc.PriceCardId == sharedCard.PriceCardId);

        Assert.NotNull(unmutatedSharedCard);
        var sharedLine = unmutatedSharedCard.Lines.FirstOrDefault(l => l.ProductId == productId);
        Assert.NotNull(sharedLine);
        Assert.Equal(95m, sharedLine.UnitPrice); // Original price preserved!

        // Verify that a new customer-specific Price Card was created
        var updatedCustomer = await verifyDb.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        Assert.NotNull(updatedCustomer!.DefaultSalesPriceCardId);
        Assert.NotEqual(sharedCard.PriceCardId, updatedCustomer.DefaultSalesPriceCardId.Value);

        var customerSpecificCard = await verifyDb.PriceCards
            .Include(pc => pc.Lines)
            .FirstOrDefaultAsync(pc => pc.PriceCardId == updatedCustomer.DefaultSalesPriceCardId.Value);

        Assert.NotNull(customerSpecificCard);
        var expectedCardCode = $"SPC-{updatedCustomer.CustomerCode}-{quotation.Currency}";
        if (expectedCardCode.Length > 50) expectedCardCode = expectedCardCode[..50];
        Assert.Equal(expectedCardCode, customerSpecificCard.PriceCardCode, ignoreCase: true);
        Assert.Equal(80m, customerSpecificCard.Lines.First().UnitPrice);
    }

    [Fact]
    public async Task CustomerAcceptance_WhenDeliveryAddressInvalid_RollsBackCleanlyWithoutPartialWrites()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var employeeUserId = TestConstants.BranchUserA1Id;
        var customerUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-ROLLBACK-1", "Iota Rollback Ltd");
        var productId = await SeedProductAsync(db, tenantId, "PROD-ROLLBACK-1", "Standard Fastener", 10m);

        var quotation = SalesQuotation.Create(
            tenantId,
            companyId,
            branchId,
            customerId,
            $"SQ-ROL-{Guid.NewGuid():N}"[..18],
            validUntilDateUtc: DateTime.UtcNow.AddDays(15),
            documentType: SalesQuotationType.Standard,
            createdByUserId: employeeUserId);

        quotation.AddItem(productId, 100m, 8m);
        quotation.SubmitForApproval(employeeUserId);
        quotation.Approve(Guid.NewGuid());
        quotation.MarkSent();
        db.SalesQuotations.Add(quotation);
        await db.SaveChangesAsync();

        var customerClient = CreateCustomerClient(
            customerUserId, "jane@example.com", tenantId, customerId, contactId,
            new[] { "CUSTOMER_COMPANY_ADMIN" }, new[] { "PORTAL_QUOTATION.ACCEPT" });

        // Pass an address ID that does not belong to this customer
        var invalidAddressId = Guid.NewGuid();
        var response = await customerClient.PostAsJsonAsync(
            $"/api/portal/customer/quotations/{quotation.Id}/accept",
            new { deliveryAddressId = invalidAddressId });

        Assert.False(response.IsSuccessStatusCode);

        // Verify transaction rolled back: quotation remains Sent, no sales order, no price card
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var uncommittedQuote = await verifyDb.SalesQuotations.FirstOrDefaultAsync(q => q.Id == quotation.Id);
        Assert.NotNull(uncommittedQuote);
        Assert.Equal(SalesQuotationStatus.Sent, uncommittedQuote.Status);
        Assert.Null(uncommittedQuote.ConvertedSalesOrderId);
        Assert.Null(uncommittedQuote.AcceptedAtUtc);

        var ordersCount = await verifyDb.SalesOrders.CountAsync(o => o.CustomerId == customerId);
        Assert.Equal(0, ordersCount);
    }

    [Fact]
    public async Task PrepareCommercialQuotation_FromCustomerRequest_PreservesOriginalRequestAndCreatesSeparateDraft()
    {
        using var scope = _fixture.Services.CreateScope();
        var quotationService = scope.ServiceProvider.GetRequiredService<ISalesQuotationService>();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();

        var tenantId = TestConstants.TenantAlphaId;
        var companyId = TestConstants.CompanyAId;
        var branchId = TestConstants.BranchA1Id;
        var staffUserId = Guid.NewGuid();

        var (customerId, contactId) = await SeedCustomerAndContactAsync(db, tenantId, "CUST-REQ-1", "Precision Engineering Ltd");
        var productId = await SeedProductAsync(db, tenantId, "PROD-REQ-1", "Custom Steel Flange", 75.00m);

        // Stage 1: Customer submits quotation request
        var createRequest = new CreateCustomerQuotationRequest
        {
            CustomerId = customerId,
            CustomerContactId = contactId,
            CustomerRemarks = "Target delivery within 14 days, need 50 units",
            RequiredDeliveryDate = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new() { ProductId = productId, Quantity = 50m, UnitPrice = 75.00m, Notes = "Grade A Spec" }
            }
        };

        var requestDto = await quotationService.CreateCustomerQuotationRequestAsync(
            tenantId, companyId, branchId, createRequest, actorUserId: contactId, actorUserName: "B2B Portal Contact");

        Assert.Equal(SalesQuotationType.CustomerRequest, requestDto.DocumentType);
        Assert.Equal("Submitted", requestDto.RequestReviewStatus);
        Assert.Null(requestDto.LinkedCommercialQuotationId);

        // Stage 2 & 3: Sales Staff prepares formal Commercial Quotation from request
        var commercialDto = await quotationService.PrepareCommercialQuotationFromRequestAsync(
            tenantId, companyId, branchId, requestDto.Id, staffUserId, "Sales Staff Officer");

        Assert.NotNull(commercialDto);
        Assert.NotEqual(requestDto.Id, commercialDto.Id);
        Assert.Equal(SalesQuotationType.Standard, commercialDto.DocumentType);
        Assert.Equal(SalesQuotationStatus.Draft, commercialDto.Status);
        Assert.Equal(requestDto.Id, commercialDto.LinkedCustomerRequestId);
        Assert.Equal(requestDto.QuotationNumber, commercialDto.LinkedCustomerRequestNumber);
        Assert.Single(commercialDto.Items);
        Assert.Equal(50m, commercialDto.Items[0].Quantity);

        // Verify Original Customer Request was preserved and linked
        var reloadedRequest = await quotationService.GetQuotationAsync(tenantId, companyId, branchId, requestDto.Id);
        Assert.NotNull(reloadedRequest);
        Assert.Equal(SalesQuotationType.CustomerRequest, reloadedRequest.DocumentType);
        Assert.Equal(commercialDto.Id, reloadedRequest.LinkedCommercialQuotationId);
        Assert.Equal(commercialDto.QuotationNumber, reloadedRequest.LinkedCommercialQuotationNumber);
        Assert.Equal("QuotationPrepared", reloadedRequest.RequestReviewStatus);
        Assert.Equal("Target delivery within 14 days, need 50 units", reloadedRequest.CustomerRemarks);
    }
}
