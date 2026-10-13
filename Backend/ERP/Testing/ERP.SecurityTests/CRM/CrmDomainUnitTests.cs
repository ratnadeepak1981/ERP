using System;
using ERP.Domain.Common.Scope;
using ERP.Domain.Features.CRM;
using Xunit;

namespace ERP.SecurityTests.CRM;

public class CrmDomainUnitTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Lead_Creation_SetsInitialState_And_EnforcesGuards()
    {
        var lead = Lead.Create(
            _tenantId,
            _companyId,
            "LD-001",
            "Wholesale Electronics Inquiry",
            "Alice Smith",
            _branchId,
            "Acme Corp",
            "alice@acme.test",
            "+1234567890",
            LeadSource.Website,
            _userId,
            15000.50m,
            "USD",
            "GoogleAds",
            "cpc",
            "CAMP-2026",
            "https://acme.test",
            "High intent buyer");

        Assert.Equal(_tenantId, lead.TenantId);
        Assert.Equal(_companyId, lead.CompanyId);
        Assert.Equal(_branchId, lead.BranchId);
        Assert.Equal("LD-001", lead.LeadNumber);
        Assert.Equal("Wholesale Electronics Inquiry", lead.Title);
        Assert.Equal("Acme Corp", lead.CompanyName);
        Assert.Equal("Alice Smith", lead.ContactName);
        Assert.Equal("alice@acme.test", lead.Email);
        Assert.Equal("+1234567890", lead.Phone);
        Assert.Equal(LeadSource.Website, lead.LeadSource);
        Assert.Equal(LeadStatus.New, lead.Status);
        Assert.Equal(_userId, lead.AssignedToUserId);
        Assert.Equal(15000.50m, lead.EstimatedValue);
        Assert.Equal("USD", lead.Currency);
        Assert.Equal("GoogleAds", lead.CampaignSource);
        Assert.Equal("CAMP-2026", lead.CampaignCode);
        Assert.True(lead.IsActive);

        // Validation guards
        Assert.Throws<ArgumentException>(() => Lead.Create(Guid.Empty, _companyId, "LD-X", "T", "C"));
        Assert.Throws<ArgumentException>(() => Lead.Create(_tenantId, Guid.Empty, "LD-X", "T", "C"));
        Assert.Throws<ArgumentException>(() => Lead.Create(_tenantId, _companyId, "", "T", "C"));
        Assert.Throws<ArgumentException>(() => Lead.Create(_tenantId, _companyId, "LD-X", "", "C"));
        Assert.Throws<ArgumentException>(() => Lead.Create(_tenantId, _companyId, "LD-X", "T", ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lead.Create(_tenantId, _companyId, "LD-X", "T", "C", estimatedValue: -100));
    }

    [Fact]
    public void Lead_StateTransitions_Qualify_Disqualify_Reopen()
    {
        var lead = Lead.Create(_tenantId, _companyId, "LD-002", "Inquiry", "Bob");

        Assert.Equal(LeadStatus.New, lead.Status);

        lead.MarkContacted();
        Assert.Equal(LeadStatus.Contacted, lead.Status);

        lead.Qualify();
        Assert.Equal(LeadStatus.Qualified, lead.Status);
        Assert.NotNull(lead.QualifiedAt);
        Assert.Null(lead.DisqualifiedAt);

        lead.Disqualify("Budget unavailable");
        Assert.Equal(LeadStatus.Disqualified, lead.Status);
        Assert.NotNull(lead.DisqualifiedAt);
        Assert.Equal("Budget unavailable", lead.DisqualificationReason);

        lead.Reopen();
        Assert.Equal(LeadStatus.New, lead.Status);
        Assert.Null(lead.DisqualifiedAt);
        Assert.Null(lead.DisqualificationReason);
    }

    [Fact]
    public void Lead_ConversionToCustomer_SetsCustomerLink_And_BlocksReconversion()
    {
        var lead = Lead.Create(_tenantId, _companyId, "LD-003", "Commercial Account", "Charlie");
        var customerId = Guid.NewGuid();

        lead.ConvertToCustomer(customerId);

        Assert.Equal(LeadStatus.Converted, lead.Status);
        Assert.Equal(customerId, lead.ConvertedCustomerId);
        Assert.NotNull(lead.ConvertedAt);
        Assert.NotNull(lead.QualifiedAt);

        // Cannot convert twice
        Assert.Throws<InvalidOperationException>(() => lead.ConvertToCustomer(Guid.NewGuid()));
        // Cannot disqualify or re-qualify converted lead
        Assert.Throws<InvalidOperationException>(() => lead.Disqualify("Reason"));
        Assert.Throws<InvalidOperationException>(() => lead.Qualify());
        Assert.Throws<InvalidOperationException>(() => lead.Reopen());
    }

    [Fact]
    public void Opportunity_CreationAndStageTransitions_CalculatesProbability()
    {
        var customerId = Guid.NewGuid();
        var opp = Opportunity.Create(
            _tenantId,
            _companyId,
            customerId,
            "OPP-001",
            "Fleet vehicle distribution deal",
            500000m,
            _branchId,
            null,
            OpportunityStage.Prospecting,
            10m);

        Assert.Equal(OpportunityStage.Prospecting, opp.Stage);
        Assert.Equal(10m, opp.ProbabilityPercent);

        // Advancing stages updates probability defaults
        opp.UpdateStage(OpportunityStage.NeedsAnalysis);
        Assert.Equal(50m, opp.ProbabilityPercent);

        opp.UpdateStage(OpportunityStage.Proposal);
        Assert.Equal(75m, opp.ProbabilityPercent);

        opp.CloseWon();
        Assert.Equal(OpportunityStage.ClosedWon, opp.Stage);
        Assert.Equal(100m, opp.ProbabilityPercent);
        Assert.NotNull(opp.ActualCloseDate);
        Assert.Null(opp.LossReason);

        opp.CloseLost("Competitor pricing lower");
        Assert.Equal(OpportunityStage.ClosedLost, opp.Stage);
        Assert.Equal(0m, opp.ProbabilityPercent);
        Assert.Equal("Competitor pricing lower", opp.LossReason);
    }

    [Fact]
    public void CrmActivity_Creation_RequiresAtLeastOneEntityLink()
    {
        var customerId = Guid.NewGuid();

        // Valid creation with Customer
        var actCustomer = CrmActivity.Create(_tenantId, _companyId, CrmActivityType.Meeting, "Client quarterly review", customerId: customerId);
        Assert.Equal(customerId, actCustomer.CustomerId);
        Assert.False(actCustomer.IsCompleted);

        // Valid creation with Lead
        var leadId = Guid.NewGuid();
        var actLead = CrmActivity.Create(_tenantId, _companyId, CrmActivityType.PhoneCall, "Discovery call", leadId: leadId);
        Assert.Equal(leadId, actLead.LeadId);

        // Invalid: missing all entity links
        Assert.Throws<ArgumentException>(() => CrmActivity.Create(_tenantId, _companyId, CrmActivityType.Task, "Orphan activity"));
    }

    [Fact]
    public void CrmActivity_CompleteAndReopen_MutatesStateCorrectly()
    {
        var customerId = Guid.NewGuid();
        var act = CrmActivity.Create(_tenantId, _companyId, CrmActivityType.Task, "Send technical brochure", customerId: customerId);

        Assert.False(act.IsCompleted);
        Assert.Null(act.CompletedAt);

        act.MarkCompleted("Brochure sent via email");
        Assert.True(act.IsCompleted);
        Assert.NotNull(act.CompletedAt);
        Assert.Equal("Brochure sent via email", act.CompletionNotes);

        act.Reopen();
        Assert.False(act.IsCompleted);
        Assert.Null(act.CompletedAt);
        Assert.Null(act.CompletionNotes);
    }

    [Fact]
    public void EntityScopePolicy_CRMEntities_AreStrictlyCompanyScoped()
    {
        // Enforce that CRM entities follow the fail-closed company scope policy
        Assert.Equal(DataScope.Company, EntityScopePolicy.GetScope<Lead>());
        Assert.Equal(DataScope.Company, EntityScopePolicy.GetScope<Opportunity>());
        Assert.Equal(DataScope.Company, EntityScopePolicy.GetScope<CrmActivity>());
    }
}
