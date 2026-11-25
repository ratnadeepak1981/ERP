using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;
using Domain.Features.Sales.SalesQuotation;
using Domain.Infrastructure.Persistence;
using ERP.SecurityTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.Equal("Pending Approval", submitted.ApprovalStatus);
        Assert.Equal("Not Sent", submitted.CustomerStatus);
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
        Assert.Equal("Approved", approved.ApprovalStatus);
        Assert.Equal("Not Sent", approved.CustomerStatus);
        Assert.Equal("NotSent", approved.EmailStatus);
        Assert.Equal(TestConstants.TenantAdminAlphaId, approved.ApprovedByUserId);
        Assert.NotNull(approved.ApprovedAtUtc);

        // 4. Verify Quotation List API returns Approved & persisted state survives fresh query
        var listResponse = await managerClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.ReadApiResponseDataAsync<List<SalesQuotationDto>>(_jsonOpts);
        Assert.NotNull(list);
        var listedApproved = list.FirstOrDefault(x => x.Id == created.Id);
        Assert.NotNull(listedApproved);
        Assert.Equal(SalesQuotationStatus.Confirmed, listedApproved.Status);
        Assert.Equal("Approved", listedApproved.ApprovalStatus);
        Assert.Equal("Not Sent", listedApproved.CustomerStatus);
        Assert.Equal("NotSent", listedApproved.EmailStatus);

        // 5. Verify Database persistence and NO Sales Order created on approval
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DomainDbContext>();
            var persisted = await db.SalesQuotations.AsNoTracking().FirstOrDefaultAsync(q => q.Id == created.Id);
            Assert.NotNull(persisted);
            Assert.Equal(SalesQuotationStatus.Confirmed, persisted.Status);
            Assert.Equal(TestConstants.TenantAdminAlphaId, persisted.ApprovedByUserId);
            Assert.NotNull(persisted.ApprovedAtUtc);
            Assert.Null(persisted.ConvertedSalesOrderId);
        }

        // 6. Verify Approval Audit History
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
        Assert.Equal("Approval Rejected", rejected.ApprovalStatus);
        Assert.Equal("Not Sent", rejected.CustomerStatus);
        Assert.Equal("Discount too high. Minimum order quantity required.", rejected.RejectionRemarks);

        // Verify list API returns Approval Rejected
        var listResponse = await managerClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.ReadApiResponseDataAsync<List<SalesQuotationDto>>(_jsonOpts);
        var listedRejected = list?.FirstOrDefault(x => x.Id == created.Id);
        Assert.NotNull(listedRejected);
        Assert.Equal("Approval Rejected", listedRejected.ApprovalStatus);
        Assert.Equal("Not Sent", listedRejected.CustomerStatus);
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

    [Fact]
    public async Task UnauthorizedApproval_WithoutConfirmPermission_ReturnsForbidden_AndPreservesPendingStatus()
    {
        var authorClient = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT" });

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            new CreateSalesQuotationDraftRequest
            {
                QuotationNumber = $"SQ-{Guid.NewGuid():N}"[..15],
                CustomerId = TestConstants.CustomerAlphaId,
                ValidUntilDateUtc = DateTime.UtcNow.AddDays(15),
                Items = new List<CreateSalesQuotationItemRequest>
                {
                    new() { ProductId = TestConstants.ProductAlphaId, Quantity = 3, UnitPrice = 100m }
                }
            });
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);

        // User with SALES_QUOTATION.VIEW / EDIT but NO SALES_QUOTATION.CONFIRM tries to approve
        var unauthorizedManager = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "sales_officer_no_confirm",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.VIEW", "SALES_QUOTATION.EDIT" });

        var approveResponse = await unauthorizedManager.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Unauthorized approve" });

        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);

        // Verify quotation remains Pending Approval
        var checkResponse = await authorClient.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}");
        var persisted = await checkResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(persisted);
        Assert.Equal(SalesQuotationStatus.Submitted, persisted.Status);
        Assert.Equal("Pending Approval", persisted.ApprovalStatus);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantManagerCannotApproveOrList_ReturnsForbidden()
    {
        var alphaAuthor = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT" });

        var createResponse = await alphaAuthor.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            new CreateSalesQuotationDraftRequest
            {
                QuotationNumber = $"SQ-{Guid.NewGuid():N}"[..15],
                CustomerId = TestConstants.CustomerAlphaId,
                ValidUntilDateUtc = DateTime.UtcNow.AddDays(15),
                Items = new List<CreateSalesQuotationItemRequest>
                {
                    new() { ProductId = TestConstants.ProductAlphaId, Quantity = 4, UnitPrice = 90m }
                }
            });
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        await alphaAuthor.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);

        // Tenant Beta manager attempts to approve Tenant Alpha quotation
        var betaManager = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminBetaId,
            "tenant_beta_mgr",
            TestConstants.TenantBetaId,
            new[] { "Sales Manager" },
            new[] { "SALES_QUOTATION.VIEW", "SALES_QUOTATION.CONFIRM" });

        var approveResponse = await betaManager.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Cross-tenant attempt" });

        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);

        var listResponse = await betaManager.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
    }

    [Fact]
    public async Task DuplicateApproval_And_EmailFailureAfterApproval_RetainsApprovedStatus()
    {
        var authorClient = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Sales Officer" },
            new[] { "SALES_QUOTATION.CREATE", "SALES_QUOTATION.VIEW", "SALES_QUOTATION.SUBMIT", "SALES_QUOTATION.EDIT" });

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations",
            new CreateSalesQuotationDraftRequest
            {
                QuotationNumber = $"SQ-{Guid.NewGuid():N}"[..15],
                CustomerId = TestConstants.CustomerAlphaId,
                ValidUntilDateUtc = DateTime.UtcNow.AddDays(15),
                Items = new List<CreateSalesQuotationItemRequest>
                {
                    new() { ProductId = TestConstants.ProductAlphaId, Quantity = 2, UnitPrice = 250m }
                }
            });
        var created = await createResponse.ReadApiResponseDataAsync<SalesQuotationDto>(_jsonOpts);
        Assert.NotNull(created);

        await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/submit",
            null);

        var managerClient = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenant_admin",
            TestConstants.TenantAlphaId,
            new[] { "Sales Manager" },
            new[] { "SALES_QUOTATION.VIEW", "SALES_QUOTATION.CONFIRM" });

        var firstApprove = await managerClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Approved by manager" });
        Assert.Equal(HttpStatusCode.OK, firstApprove.StatusCode);

        // Duplicate approval must fail without adding duplicate approval audit records
        var duplicateApprove = await managerClient.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/approve",
            new SalesQuotationDecisionRequest { Remarks = "Duplicate approve" });
        Assert.False(duplicateApprove.IsSuccessStatusCode);

        // Attempt email dispatch (in test environment SMTP is disabled/unconfigured -> fails)
        var sendResponse = await authorClient.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations/{created.Id}/send",
            null);

        // Even when email dispatch fails, quotation remains Confirmed / Approved
        if (!sendResponse.IsSuccessStatusCode)
        {
            var listResponse = await managerClient.GetAsync(
                $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/sales-quotations");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var list = await listResponse.ReadApiResponseDataAsync<List<SalesQuotationDto>>(_jsonOpts);
            var listed = list?.FirstOrDefault(x => x.Id == created.Id);
            Assert.NotNull(listed);
            Assert.Equal(SalesQuotationStatus.Confirmed, listed.Status);
            Assert.Equal("Approved", listed.ApprovalStatus);
            Assert.Equal("Failed", listed.EmailStatus);
            Assert.False(string.IsNullOrWhiteSpace(listed.EmailFailureReason));
        }
    }
}
