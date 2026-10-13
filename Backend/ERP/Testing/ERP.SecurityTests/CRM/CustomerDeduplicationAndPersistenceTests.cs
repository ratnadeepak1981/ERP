using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Features.MasterData.Branch;
using Domain.Features.MasterData.Company;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.CRM;
using ERP.Domain.Features.CRM.Deduplication;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.SecurityTests.CRM;

public class CustomerDeduplicationAndPersistenceTests
{
    private static DomainDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>()
            .UseInMemoryDatabase(databaseName: $"CRM_Test_{Guid.NewGuid():N}")
            .Options;

        return new DomainDbContext(options);
    }

    [Fact]
    public async Task Deduplication_ExactTaxNumber_ReturnsExactMatch()
    {
        await using var db = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();

        var existingCustomer = Customer.Create(
            tenantId,
            "CUST-001",
            "Apex Logistics Ltd",
            email: "contact@apex.test",
            taxNumber: "TAX-998877");
        db.Customers.Add(existingCustomer);
        await db.SaveChangesAsync();

        var deduplicationService = new CustomerDeduplicationService(db);

        var criteria = new CustomerMatchCriteria
        {
            TaxNumber = "TAX-998877",
            CustomerName = "Completely Different Name"
        };

        var result = await deduplicationService.FindDuplicateAsync(tenantId, criteria);

        Assert.Equal(CustomerMatchConfidence.ExactMatch, result.Confidence);
        Assert.Equal(CustomerMatchType.TaxNumberExact, result.MatchType);
        Assert.Equal(existingCustomer.CustomerId, result.MatchedCustomerId);
        Assert.Equal(existingCustomer.CustomerCode, result.MatchedCustomerCode);
    }

    [Fact]
    public async Task Deduplication_ExactEmail_ReturnsExactMatch()
    {
        await using var db = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();

        var existingCustomer = Customer.Create(
            tenantId,
            "CUST-002",
            "Zenith Trading",
            email: "info@zenith.test",
            taxNumber: "TAX-111");
        db.Customers.Add(existingCustomer);
        await db.SaveChangesAsync();

        var deduplicationService = new CustomerDeduplicationService(db);

        var criteria = new CustomerMatchCriteria
        {
            Email = "INFO@ZENITH.TEST", // Case-insensitive
            CustomerName = "Zenith Brand"
        };

        var result = await deduplicationService.FindDuplicateAsync(tenantId, criteria);

        Assert.Equal(CustomerMatchConfidence.ExactMatch, result.Confidence);
        Assert.Equal(CustomerMatchType.EmailExact, result.MatchType);
        Assert.Equal(existingCustomer.CustomerId, result.MatchedCustomerId);
    }

    [Fact]
    public async Task Deduplication_PhoneAndNameMatches_ReturnPotentialMatch()
    {
        await using var db = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();

        var customerByPhone = Customer.Create(
            tenantId,
            "CUST-003",
            "Northstar Media",
            phone: "+1-555-888-9999");
        db.Customers.Add(customerByPhone);

        var customerByName = Customer.Create(
            tenantId,
            "CUST-004",
            "Summit Solutions",
            phone: "+1-555-000-1111");
        db.Customers.Add(customerByName);

        await db.SaveChangesAsync();

        var deduplicationService = new CustomerDeduplicationService(db);

        // Test Phone match
        var phoneCriteria = new CustomerMatchCriteria { Phone = "15558889999" };
        var phoneResult = await deduplicationService.FindDuplicateAsync(tenantId, phoneCriteria);
        Assert.Equal(CustomerMatchConfidence.PotentialMatch, phoneResult.Confidence);
        Assert.Equal(CustomerMatchType.PhoneExact, phoneResult.MatchType);
        Assert.Equal(customerByPhone.CustomerId, phoneResult.MatchedCustomerId);

        // Test Name match
        var nameCriteria = new CustomerMatchCriteria { CustomerName = "SUMMIT SOLUTIONS" };
        var nameResult = await deduplicationService.FindDuplicateAsync(tenantId, nameCriteria);
        Assert.Equal(CustomerMatchConfidence.PotentialMatch, nameResult.Confidence);
        Assert.Equal(CustomerMatchType.CustomerNameNormalized, nameResult.MatchType);
        Assert.Equal(customerByName.CustomerId, nameResult.MatchedCustomerId);
    }

    [Fact]
    public async Task Deduplication_CrossTenant_DeniesMatchAcrossTenants()
    {
        await using var db = CreateInMemoryContext();
        var tenantAlpha = Guid.NewGuid();
        var tenantBeta = Guid.NewGuid();

        // Customer exists strictly in Tenant Beta
        var customerBeta = Customer.Create(
            tenantBeta,
            "CUST-BETA",
            "Beta Corp",
            email: "same_email@enterprise.test",
            taxNumber: "SHARED-TAX-12345");
        db.Customers.Add(customerBeta);
        await db.SaveChangesAsync();

        var deduplicationService = new CustomerDeduplicationService(db);

        // Inquiry in Tenant Alpha with exact same tax number and email
        var criteria = new CustomerMatchCriteria
        {
            TaxNumber = "SHARED-TAX-12345",
            Email = "same_email@enterprise.test",
            CustomerName = "Beta Corp"
        };

        var result = await deduplicationService.FindDuplicateAsync(tenantAlpha, criteria);

        // Must NOT match customer in Tenant Beta
        Assert.Equal(CustomerMatchConfidence.None, result.Confidence);
        Assert.Equal(CustomerMatchType.None, result.MatchType);
        Assert.Null(result.MatchedCustomerId);
    }

    [Fact]
    public async Task Deduplication_InactiveCustomer_IsIgnored()
    {
        await using var db = CreateInMemoryContext();
        var tenantId = Guid.NewGuid();

        var inactiveCustomer = Customer.Create(
            tenantId,
            "CUST-INACTIVE",
            "Defunct Corp",
            email: "old@defunct.test",
            taxNumber: "TAX-DEFUNCT");
        inactiveCustomer.Deactivate();

        db.Customers.Add(inactiveCustomer);
        await db.SaveChangesAsync();

        var deduplicationService = new CustomerDeduplicationService(db);

        var criteria = new CustomerMatchCriteria
        {
            TaxNumber = "TAX-DEFUNCT",
            Email = "old@defunct.test"
        };

        var result = await deduplicationService.FindDuplicateAsync(tenantId, criteria);
        Assert.Equal(CustomerMatchConfidence.None, result.Confidence);
    }

    [Fact]
    public async Task CRM_Persistence_TenantAndCompanyIsolation_Enforced()
    {
        await using var db = CreateInMemoryContext();
        var tenantAlpha = Guid.NewGuid();
        var tenantBeta = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var leadA = Lead.Create(tenantAlpha, companyA, "LD-A1", "Alpha Inquiry", "Alice");
        var leadB = Lead.Create(tenantBeta, companyB, "LD-B1", "Beta Inquiry", "Bob");

        db.Leads.AddRange(leadA, leadB);
        await db.SaveChangesAsync();

        // Querying Tenant Alpha returns only Alpha leads
        var alphaLeads = await db.Leads.Where(l => l.TenantId == tenantAlpha).ToListAsync();
        Assert.Single(alphaLeads);
        Assert.Equal("LD-A1", alphaLeads[0].LeadNumber);

        // Opportunities and Activities persistence
        var customerA = Customer.Create(tenantAlpha, "CUST-A", "Customer Alpha");
        db.Customers.Add(customerA);
        await db.SaveChangesAsync();

        var oppA = Opportunity.Create(tenantAlpha, companyA, customerA.CustomerId, "OPP-A1", "Deal A", 25000m, leadId: leadA.LeadId);
        var actA = CrmActivity.Create(tenantAlpha, companyA, CrmActivityType.Task, "Call Alice", leadId: leadA.LeadId, customerId: customerA.CustomerId);

        db.Opportunities.Add(oppA);
        db.CrmActivities.Add(actA);
        await db.SaveChangesAsync();

        var loadedOpp = await db.Opportunities.FirstOrDefaultAsync(o => o.TenantId == tenantAlpha && o.OpportunityId == oppA.OpportunityId);
        Assert.NotNull(loadedOpp);
        Assert.Equal("Deal A", loadedOpp.Title);
        Assert.Equal(leadA.LeadId, loadedOpp.LeadId);

        var loadedAct = await db.CrmActivities.FirstOrDefaultAsync(a => a.TenantId == tenantAlpha && a.CrmActivityId == actA.CrmActivityId);
        Assert.NotNull(loadedAct);
        Assert.Equal("Call Alice", loadedAct.Subject);
    }
}
