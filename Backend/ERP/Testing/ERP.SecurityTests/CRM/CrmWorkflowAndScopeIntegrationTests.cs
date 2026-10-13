using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Infrastructure.Persistence;
using ERP.Domain.Features.CRM;
using ERP.Domain.Features.MasterData.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.SecurityTests.CRM;

public class CrmWorkflowAndScopeIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CrmWorkflowAndScopeIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task Lead_FullLifecycle_Create_Qualify_And_Convert_To_Customer()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.LEAD.VIEW", "CRM.LEAD.CREATE", "CRM.LEAD.QUALIFY", "CRM.LEAD.CONVERT" });

        var uniqueNumber = $"LEAD_{Guid.NewGuid():N}"[..12];
        var createReq = new CreateLeadRequest
        {
            LeadNumber = uniqueNumber,
            Title = "Retail Expansion Prospect",
            ContactName = "Jane Doe",
            CompanyName = "Doe Enterprises",
            Email = $"jane_{Guid.NewGuid():N}@enterprise.test",
            Phone = "+1-555-443-2211",
            EstimatedValue = 50000m,
            LeadSource = LeadSource.Website
        };

        // 1. Create Lead
        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads",
            createReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var createdLead = await postRes.Content.ReadFromJsonAsync<LeadDto>(_jsonOpts);
        Assert.NotNull(createdLead);
        Assert.Equal(LeadStatus.New, createdLead.Status);
        Assert.Equal(uniqueNumber, createdLead.LeadNumber);

        // 2. Qualify Lead
        var qualifyRes = await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads/{createdLead.LeadId}/qualify",
            null);
        Assert.Equal(HttpStatusCode.OK, qualifyRes.StatusCode);
        var qualifiedLead = await qualifyRes.Content.ReadFromJsonAsync<LeadDto>(_jsonOpts);
        Assert.NotNull(qualifiedLead);
        Assert.Equal(LeadStatus.Qualified, qualifiedLead.Status);
        Assert.NotNull(qualifiedLead.QualifiedAt);

        // 3. Convert Lead to Customer
        var convertReq = new ConvertLeadRequest
        {
            CustomerCode = $"CUST_{Guid.NewGuid():N}"[..10],
            CustomerName = "Doe Enterprises Corp",
            LinkToExistingIfMatched = false
        };

        var convertRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads/{createdLead.LeadId}/convert",
            convertReq);
        Assert.Equal(HttpStatusCode.OK, convertRes.StatusCode);
        var convertResult = await convertRes.Content.ReadFromJsonAsync<ConvertLeadResult>(_jsonOpts);
        Assert.NotNull(convertResult);
        Assert.False(convertResult.WasExistingCustomer);

        // Verify state in database
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var dbLead = await db.Leads.FirstOrDefaultAsync(l => l.LeadId == createdLead.LeadId);
        Assert.NotNull(dbLead);
        Assert.Equal(LeadStatus.Converted, dbLead.Status);
        Assert.Equal(convertResult.CustomerId, dbLead.ConvertedCustomerId);

        var dbCustomer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == convertResult.CustomerId);
        Assert.NotNull(dbCustomer);
        Assert.Equal(convertReq.CustomerCode, dbCustomer.CustomerCode);
        Assert.Equal(TestConstants.TenantAlphaId, dbCustomer.TenantId);
    }

    [Fact]
    public async Task Lead_Convert_Deduplication_Links_To_Existing_Customer()
    {
        var sharedEmail = $"procure_{Guid.NewGuid():N}@sharedglobal.test";

        // Seed existing customer in the test database
        using (var setupScope = _fixture.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var existingCust = Customer.Create(
                TestConstants.TenantAlphaId,
                $"CUST_{Guid.NewGuid():N}"[..10],
                "Global Traders Shared",
                contactPerson: "Alice Smith",
                email: sharedEmail,
                phone: "+1-555-777-9988");
            db.Customers.Add(existingCust);
            await db.SaveChangesAsync();
        }

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.LEAD.VIEW", "CRM.LEAD.CREATE", "CRM.LEAD.QUALIFY", "CRM.LEAD.CONVERT" });

        // Create lead with the matching email
        var createReq = new CreateLeadRequest
        {
            LeadNumber = $"LEAD_{Guid.NewGuid():N}"[..12],
            Title = "Duplicate Customer Inbound Lead",
            ContactName = "Alice Smith",
            Email = sharedEmail,
            LeadSource = LeadSource.ColdCall
        };

        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads",
            createReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var createdLead = await postRes.Content.ReadFromJsonAsync<LeadDto>(_jsonOpts);
        Assert.NotNull(createdLead);

        // Qualify
        await client.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads/{createdLead.LeadId}/qualify",
            null);

        // Convert with deduplication linking enabled
        var convertReq = new ConvertLeadRequest
        {
            CustomerCode = "NEW_CODE_SHOULD_BE_IGNORED",
            CustomerName = "Duplicate Enterprise",
            LinkToExistingIfMatched = true
        };

        var convertRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads/{createdLead.LeadId}/convert",
            convertReq);
        Assert.Equal(HttpStatusCode.OK, convertRes.StatusCode);
        var convertResult = await convertRes.Content.ReadFromJsonAsync<ConvertLeadResult>(_jsonOpts);
        Assert.NotNull(convertResult);
        Assert.True(convertResult.WasExistingCustomer);

        // Verify in DB that no second customer was inserted for this email
        using var verifyScope = _fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<DomainDbContext>();
        var matchingCustomers = await verifyDb.Customers
            .Where(c => c.TenantId == TestConstants.TenantAlphaId && c.Email == sharedEmail)
            .ToListAsync();
        Assert.Single(matchingCustomers);
        Assert.Equal(matchingCustomers[0].CustomerId, convertResult.CustomerId);
    }

    [Fact]
    public async Task Opportunity_Lifecycle_Create_And_AdvanceStage()
    {
        Guid customerId;
        using (var setupScope = _fixture.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var cust = Customer.Create(
                TestConstants.TenantAlphaId,
                $"CUST_{Guid.NewGuid():N}"[..10],
                "Opportunity Customer Corp");
            db.Customers.Add(cust);
            await db.SaveChangesAsync();
            customerId = cust.CustomerId;
        }

        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.OPPORTUNITY.VIEW", "CRM.OPPORTUNITY.MANAGE" });

        var oppNumber = $"OPP_{Guid.NewGuid():N}"[..12];
        var createReq = new CreateOpportunityRequest
        {
            OpportunityNumber = oppNumber,
            Title = "Annual Software License",
            CustomerId = customerId,
            EstimatedValue = 120000m,
            Stage = OpportunityStage.Prospecting
        };

        // Create
        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/opportunities",
            createReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var createdOpp = await postRes.Content.ReadFromJsonAsync<OpportunityDto>(_jsonOpts);
        Assert.NotNull(createdOpp);
        Assert.Equal(OpportunityStage.Prospecting, createdOpp.Stage);
        Assert.Equal(10m, createdOpp.ProbabilityPercent);

        // Advance to ClosedWon
        var stageReq = new UpdateOpportunityStageRequest
        {
            Stage = OpportunityStage.ClosedWon
        };
        var stageRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/opportunities/{createdOpp.OpportunityId}/stage",
            stageReq);
        Assert.Equal(HttpStatusCode.OK, stageRes.StatusCode);
        var updatedOpp = await stageRes.Content.ReadFromJsonAsync<OpportunityDto>(_jsonOpts);
        Assert.NotNull(updatedOpp);
        Assert.Equal(OpportunityStage.ClosedWon, updatedOpp.Stage);
        Assert.Equal(100m, updatedOpp.ProbabilityPercent);
        Assert.NotNull(updatedOpp.ActualCloseDate);
    }

    [Fact]
    public async Task CrmActivity_Create_And_Complete()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.ACTIVITY.VIEW", "CRM.ACTIVITY.MANAGE" });

        Guid customerId;
        using (var setupScope = _fixture.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var cust = Customer.Create(
                TestConstants.TenantAlphaId,
                $"CUST_{Guid.NewGuid():N}"[..10],
                "Activity Test Customer Corp");
            db.Customers.Add(cust);
            await db.SaveChangesAsync();
            customerId = cust.CustomerId;
        }

        var createReq = new CreateCrmActivityRequest
        {
            ActivityType = CrmActivityType.Meeting,
            Subject = "Q4 Alignment Meeting",
            Description = "Discuss contract terms and delivery milestones.",
            CustomerId = customerId
        };

        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/activities",
            createReq);
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var createdAct = await postRes.Content.ReadFromJsonAsync<CrmActivityDto>(_jsonOpts);
        Assert.NotNull(createdAct);
        Assert.False(createdAct.IsCompleted);

        // Complete
        var compReq = new CompleteCrmActivityRequest
        {
            CompletionNotes = "Meeting completed with agreed terms."
        };
        var compRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/activities/{createdAct.CrmActivityId}/complete",
            compReq);
        Assert.Equal(HttpStatusCode.OK, compRes.StatusCode);
        var completedAct = await compRes.Content.ReadFromJsonAsync<CrmActivityDto>(_jsonOpts);
        Assert.NotNull(completedAct);
        Assert.True(completedAct.IsCompleted);
        Assert.NotNull(completedAct.CompletedAt);
    }

    [Fact]
    public async Task CompanyScope_Denied_When_User_Accesses_Foreign_Company()
    {
        // CompanyUserA is restricted to Company A only
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.LEAD.VIEW", "CRM.LEAD.CREATE" });

        // Attempting to view Company B leads
        var getRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/crm/leads");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        // Attempting to create lead in Company B
        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyBId}/crm/leads",
            new CreateLeadRequest
            {
                LeadNumber = "TEST-DENY",
                Title = "Should Be Forbidden",
                ContactName = "No Access"
            });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
    }

    [Fact]
    public async Task BranchScope_Denied_When_BranchUser_Accesses_Foreign_Branch()
    {
        // BranchUserA1 is scoped to Branch A1
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "CRM.LEAD.VIEW", "CRM.LEAD.CREATE" });

        // Attempting to query leads scoped to Branch A2
        var getRes = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads?branchId={TestConstants.BranchA2Id}");
        Assert.Equal(HttpStatusCode.Forbidden, getRes.StatusCode);

        // Attempting to create lead targeting Branch A2
        var postRes = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads",
            new CreateLeadRequest
            {
                LeadNumber = "TEST-BRANCH-DENY",
                Title = "Should Be Forbidden",
                ContactName = "No Access",
                BranchId = TestConstants.BranchA2Id
            });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);
    }

    [Fact]
    public async Task Permission_Denied_When_User_Lacks_Required_Permission()
    {
        // User with VIEW only, lacking CREATE
        var viewOnlyClient = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.LEAD.VIEW" });

        var postRes = await viewOnlyClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads",
            new CreateLeadRequest
            {
                LeadNumber = "TEST-PERM-DENY",
                Title = "No Create Permission",
                ContactName = "Bob"
            });
        Assert.Equal(HttpStatusCode.Forbidden, postRes.StatusCode);

        // User with VIEW + CREATE, lacking CONVERT
        var noConvertClient = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "companyuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Company User" },
            new[] { "CRM.LEAD.VIEW", "CRM.LEAD.CREATE", "CRM.LEAD.QUALIFY" });

        var createRes = await noConvertClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads",
            new CreateLeadRequest
            {
                LeadNumber = $"LEAD_{Guid.NewGuid():N}"[..12],
                Title = "Qualify Allowed",
                ContactName = "Carol"
            });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var lead = await createRes.Content.ReadFromJsonAsync<LeadDto>(_jsonOpts);
        Assert.NotNull(lead);

        // Convert without permission must fail
        var convertRes = await noConvertClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads/{lead.LeadId}/convert",
            new ConvertLeadRequest
            {
                CustomerCode = "DENIED",
                CustomerName = "Denied"
            });
        Assert.Equal(HttpStatusCode.Forbidden, convertRes.StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenant_Access_Denied()
    {
        // Tenant Beta Admin attempts to access Tenant Alpha's company CRM
        var betaClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenantadmin_b",
            TestConstants.TenantBetaId,
            new[] { "Tenant Admin" },
            new[] { "CRM.LEAD.VIEW" });

        var res = await betaClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/crm/leads");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
