using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.SalesQuotation;
using ERP.SecurityTests;
using Xunit;

namespace ERP.SecurityTests.Sales;

public class SalesQuotationWorkflowAndScopeIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public SalesQuotationWorkflowAndScopeIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
        _fixture.EnsureDatabasesInitialized();
    }

    [Fact]
    public async Task CreateSalesQuotationDraft_AuthorizedBranchUser_Succeeds()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW" });

        var uniqueQuoteNum = $"SQ-{Guid.NewGuid():N}"[..15];
        var request = new CreateSalesQuotationDraftRequest
        {
            QuotationNumber = uniqueQuoteNum,
            CustomerId = TestConstants.CustomerAlphaId,
            QuotationDate = DateTime.UtcNow,
            ValidUntilDateUtc = DateTime.UtcNow.AddDays(14),
            Currency = "USD",
            Notes = "Test formal quotation",
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 10,
                    UnitPrice = 100m,
                    DiscountRate = 10m,
                    TaxRate = 5m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Equal(uniqueQuoteNum, created.QuotationNumber);
        Assert.Equal(SalesQuotationStatus.Draft, created.Status);
        Assert.Equal(SalesQuotationType.Standard, created.DocumentType);

        // SubTotal: 10 * 100 = 1000 - 100 = 900
        // Discount: 100
        // Tax: 900 * 0.05 = 45
        // Total: 945
        Assert.Equal(900.00m, created.SubTotal);
        Assert.Equal(100.00m, created.DiscountAmount);
        Assert.Equal(45.00m, created.TaxAmount);
        Assert.Equal(945.00m, created.TotalAmount);
    }

    [Fact]
    public async Task CreateSalesQuotation_WithoutCreatePermission_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.ReadOnlyUserId,
            "readonly_user",
            TestConstants.TenantAlphaId,
            new[] { "Viewer" },
            new[] { "SALES_QUOTATION.VIEW" });

        var request = new CreateSalesQuotationDraftRequest
        {
            QuotationNumber = $"SQ-NOPERM-{Guid.NewGuid():N}"[..15],
            CustomerId = TestConstants.CustomerAlphaId,
            ValidUntilDateUtc = DateTime.UtcNow.AddDays(7)
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomerQuotationRequest_AuthorizedUser_Succeeds()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW" });

        var request = new CreateCustomerQuotationRequest
        {
            CustomerId = TestConstants.CustomerAlphaId,
            CustomerRemarks = "Customer portal urgent request",
            Currency = "USD",
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 2,
                    UnitPrice = 150m
                }
            }
        };

        var response = await client.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/request",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);
        Assert.Equal(SalesQuotationType.CustomerRequest, created.DocumentType);
        Assert.Equal(SalesQuotationStatus.Draft, created.Status);
        Assert.Equal("Customer portal urgent request", created.CustomerRemarks);
    }

    [Fact]
    public async Task SalesQuotation_Submit_And_AdminApprove_Workflow_Succeeds()
    {
        // 1. Author creates draft
        var authorClient = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT" });

        var uniqueQuoteNum = $"SQ-{Guid.NewGuid():N}"[..15];
        var createRequest = new CreateSalesQuotationDraftRequest
        {
            QuotationNumber = uniqueQuoteNum,
            CustomerId = TestConstants.CustomerAlphaId,
            ValidUntilDateUtc = DateTime.UtcNow.AddDays(20),
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 5,
                    UnitPrice = 200m
                }
            }
        };

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        // 2. Author submits for approval
        var submitResponse = await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(submitted);
        Assert.Equal(SalesQuotationStatus.Submitted, submitted.Status);
        Assert.Equal(TestConstants.BranchUserA1Id, submitted.SubmittedByUserId);

        // 3. Manager/Admin approves quotation
        var managerClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenant_admin",
            TestConstants.TenantAlphaId,
            new[] { "Sales Manager" },
            new[] { "SALES_QUOTATION.VIEW", "SALES_QUOTATION.CONFIRM" });

        var approveResponse = await managerClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Proposal commercial terms approved." });

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(approved);
        Assert.Equal(SalesQuotationStatus.Confirmed, approved.Status);
        Assert.Equal(TestConstants.TenantAdminAlphaId, approved.ApprovedByUserId);

        // 4. Verify Approval Audit History
        var historyResponse = await managerClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approval-history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.ReadApiResponseDataAsync<List<ApprovalAuditRecord>>(_jsonOpts);
        Assert.NotNull(history);
        Assert.True(history.Count >= 2);
        Assert.Contains(history, r => r.Decision == ApprovalDecision.Submitted);
        Assert.Contains(history, r => r.Decision == ApprovalDecision.Approved);
    }

    [Fact]
    public async Task SalesQuotation_Reject_Workflow_Succeeds()
    {
        // 1. Author creates and submits
        var authorClient = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT" });

        var uniqueQuoteNum = $"SQ-{Guid.NewGuid():N}"[..15];
        var createRequest = new CreateSalesQuotationDraftRequest
        {
            QuotationNumber = uniqueQuoteNum,
            CustomerId = TestConstants.CustomerAlphaId,
            ValidUntilDateUtc = DateTime.UtcNow.AddDays(20),
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 1,
                    UnitPrice = 50m
                }
            }
        };

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            createRequest);
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);

        // 2. Manager rejects quotation with remarks
        var managerClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenant_admin",
            TestConstants.TenantAlphaId,
            new[] { "Sales Manager" },
            new[] { "SALES_QUOTATION.VIEW", "SALES_QUOTATION.CONFIRM" });

        var rejectResponse = await managerClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/reject",
            new SalesQuotationDecisionRequest { Remarks = "Discount too high. Minimum order quantity required." });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(rejected);
        Assert.Equal(SalesQuotationStatus.RejectedInternal, rejected.Status);
        Assert.Equal("Discount too high. Minimum order quantity required.", rejected.RejectionRemarks);
    }

    [Fact]
    public async Task SelfApproval_WhenAuthorAttemptsToApprove_IsRejected()
    {
        // Author has both SUBMIT and CONFIRM permissions
        var authorClient = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Manager" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT", "SALES_QUOTATION.CONFIRM" });

        var uniqueQuoteNum = $"SQ-{Guid.NewGuid():N}"[..15];
        var createRequest = new CreateSalesQuotationDraftRequest
        {
            QuotationNumber = uniqueQuoteNum,
            CustomerId = TestConstants.CustomerAlphaId,
            ValidUntilDateUtc = DateTime.UtcNow.AddDays(20),
            Items = new List<CreateSalesQuotationItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 2,
                    UnitPrice = 80m
                }
            }
        };

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            createRequest);
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);

        // Author attempts self-approval
        var approveResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Self approving." });

        // Should fail due to self-approval protection rule
        Assert.False(approveResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SalesQuotation_CrossScopeAccess_ReturnsForbidden()
    {
        var client = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "SALES_QUOTATION.VIEW" });

        // User belongs to Company A / Branch A1, attempts to access Company B
        var response = await client.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/sales-quotations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
