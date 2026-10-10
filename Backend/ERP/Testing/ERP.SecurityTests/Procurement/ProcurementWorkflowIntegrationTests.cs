using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Xunit;

namespace ERP.SecurityTests.Procurement;

public class ProcurementWorkflowIntegrationTests : IClassFixture<TestSecurityFixture>
{
    private readonly TestSecurityFixture _fixture;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ProcurementWorkflowIntegrationTests(TestSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TenantProcurementSettings_TenantAdminCanConfigureSettings_AdvancedModeIsRejected()
    {
        var clientAdmin = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT" });

        // 1. Get current settings
        var getRes = await clientAdmin.GetAsync("/api/tenants/current/settings/procurement-approval");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        // 2. Attempt to configure Advanced mode -> Rejected 400
        var advReq = new { ApprovalRequired = true, ApprovalMode = 2 };
        var advRes = await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", advReq);
        Assert.Equal(HttpStatusCode.BadRequest, advRes.StatusCode);

        // 3. Configure Simple mode with approval required = true
        var simpleReq = new { ApprovalRequired = true, ApprovalMode = 1 };
        var simpleRes = await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", simpleReq);
        Assert.Equal(HttpStatusCode.OK, simpleRes.StatusCode);
    }

    [Fact]
    public async Task PurchaseOrder_FullSimpleApprovalLifecycle_WithSelfApprovalProtection()
    {
        // Branch User A1 creates and submits PO
        var clientUser = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.SUBMIT" });

        // Ensure approval required = true
        var clientAdmin = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT" });
        await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", new { ApprovalRequired = true, ApprovalMode = 1 });

        // 1. Create Draft PO
        var orderNumber = $"PO-FLOW-{Guid.NewGuid():N}"[..15];
        var createReq = new CreatePurchaseOrderRequest
        {
            OrderNumber = orderNumber,
            OrderDate = DateTime.UtcNow,
            Items = new List<CreatePurchaseOrderItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 5,
                    UnitPrice = 20m
                }
            }
        };

        var createRes = await clientUser.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
            createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdOrder = await createRes.Content.ReadFromJsonAsync<PurchaseOrderResponseDto>(_jsonOpts);
        Assert.NotNull(createdOrder);
        var orderId = createdOrder.Id;

        // 2. Submit PO
        var submitRes = await clientUser.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{orderId}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
        var submittedOrder = await submitRes.Content.ReadFromJsonAsync<PurchaseOrderResponseDto>(_jsonOpts);
        Assert.Equal(PurchaseOrderStatus.Submitted, submittedOrder!.Status);

        // 3. Creator tries to approve own PO (even if given APPROVE permission) -> Prohibited / Conflict
        var clientUserWithApprove = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.APPROVE" });

        var selfApproveRes = await clientUserWithApprove.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{orderId}/approve",
            new ApprovalDecisionRequest { Remarks = "Self approving" });
        Assert.Equal(HttpStatusCode.Conflict, selfApproveRes.StatusCode);

        // 4. Company GM approves PO -> Succeeds
        var clientCompanyGM = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "compuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.APPROVE" });

        var approveRes = await clientCompanyGM.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{orderId}/approve",
            new ApprovalDecisionRequest { Remarks = "Approved by Company GM" });
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approvedOrder = await approveRes.Content.ReadFromJsonAsync<PurchaseOrderResponseDto>(_jsonOpts);
        Assert.Equal(PurchaseOrderStatus.Approved, approvedOrder!.Status);

        // 5. Verify Approval History
        var historyRes = await clientCompanyGM.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{orderId}/approval-history");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
        var history = await historyRes.Content.ReadFromJsonAsync<List<ApprovalAuditRecordDto>>(_jsonOpts);
        Assert.NotNull(history);
        Assert.Equal(2, history.Count); // Submitted + Approved
        Assert.False(history[0].IsAutomated);
        Assert.False(history[1].IsAutomated);
    }

    [Fact]
    public async Task PurchaseOrder_WhenApprovalDisabled_AutoApprovesOnSubmit()
    {
        var clientAdmin = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT" });

        // Set approval required = false
        var putRes = await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", new { ApprovalRequired = false, ApprovalMode = 1 });
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        try
        {
            var clientUser = _fixture.CreateAuthenticatedClient(
                TestConstants.BranchUserA1Id,
                "branchuser_a1",
                TestConstants.TenantAlphaId,
                new[] { "Branch User" },
                new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE", "PURCHASE_ORDER.SUBMIT" });

            var orderNumber = $"PO-NOAPP-{Guid.NewGuid():N}"[..15];
            var createRes = await clientUser.PostAsJsonAsync(
                $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders",
                new CreatePurchaseOrderRequest
                {
                    OrderNumber = orderNumber,
                    Items = new List<CreatePurchaseOrderItemRequest>
                    {
                        new() { ProductId = TestConstants.ProductAlphaId, Quantity = 2, UnitPrice = 15m }
                    }
                });
            var order = await createRes.Content.ReadFromJsonAsync<PurchaseOrderResponseDto>(_jsonOpts);

            // Submit -> Should immediately AutoApprove
            var submitRes = await clientUser.PostAsync(
                $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{order!.Id}/submit",
                null);
            Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
            var submittedOrder = await submitRes.Content.ReadFromJsonAsync<PurchaseOrderResponseDto>(_jsonOpts);
            Assert.Equal(PurchaseOrderStatus.Approved, submittedOrder!.Status);

            // Verify history shows automated approval
            var historyRes = await clientUser.GetAsync(
                $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders/{order.Id}/approval-history");
            var history = await historyRes.Content.ReadFromJsonAsync<List<ApprovalAuditRecordDto>>(_jsonOpts);
            Assert.NotNull(history);
            Assert.Single(history);
            Assert.True(history[0].IsAutomated);
        }
        finally
        {
            // Restore approval required = true for other tests
            await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", new { ApprovalRequired = true, ApprovalMode = 1 });
        }
    }

    [Fact]
    public async Task PurchaseRequisition_CreateSubmitRejectLifecycle_AndBranchScopeEnforcement()
    {
        var clientAdmin = _fixture.CreateAuthenticatedClient(
            TestConstants.TenantAdminAlphaId,
            "tenantadmin_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PROCUREMENT.SETTINGS.VIEW", "PROCUREMENT.SETTINGS.EDIT" });
        await clientAdmin.PutAsJsonAsync("/api/tenants/current/settings/procurement-approval", new { ApprovalRequired = true, ApprovalMode = 1 });

        var clientBranchA1 = _fixture.CreateAuthenticatedClient(
            TestConstants.BranchUserA1Id,
            "branchuser_a1",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.CREATE", "PURCHASE_REQUISITION.SUBMIT" });

        // 1. Create PR
        var reqNum = $"PR-{Guid.NewGuid():N}"[..14];
        var createReq = new CreatePurchaseRequisitionRequest
        {
            RequisitionNumber = reqNum,
            RequiredDate = DateTime.UtcNow.AddDays(7),
            Notes = "Office consumables",
            Items = new List<CreatePurchaseRequisitionItemRequest>
            {
                new()
                {
                    ProductId = TestConstants.ProductAlphaId,
                    Quantity = 10,
                    EstimatedUnitPrice = 5m,
                    Remarks = "Urgent"
                }
            }
        };

        var createRes = await clientBranchA1.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-requisitions",
            createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdPR = await createRes.Content.ReadFromJsonAsync<PurchaseRequisitionResponseDto>(_jsonOpts);
        Assert.NotNull(createdPR);
        Assert.Equal(50m, createdPR.TotalAmount);

        // 2. Submit PR
        var submitRes = await clientBranchA1.PostAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-requisitions/{createdPR.Id}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        // 3. Rejection requires remarks
        var clientCompanyGM = _fixture.CreateAuthenticatedClient(
            TestConstants.CompanyUserAId,
            "compuser_a",
            TestConstants.TenantAlphaId,
            new[] { "Tenant Admin" },
            new[] { "PURCHASE_REQUISITION.VIEW", "PURCHASE_REQUISITION.APPROVE" });

        var rejectNoRemarks = await clientCompanyGM.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-requisitions/{createdPR.Id}/reject",
            new ApprovalDecisionRequest { Remarks = "" });
        Assert.Equal(HttpStatusCode.BadRequest, rejectNoRemarks.StatusCode);

        // 4. Reject with valid remarks
        var rejectValid = await clientCompanyGM.PostAsJsonAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-requisitions/{createdPR.Id}/reject",
            new ApprovalDecisionRequest { Remarks = "Budget exhausted for this quarter." });
        Assert.Equal(HttpStatusCode.OK, rejectValid.StatusCode);
        var rejectedPR = await rejectValid.Content.ReadFromJsonAsync<PurchaseRequisitionResponseDto>(_jsonOpts);
        Assert.Equal(PurchaseRequisitionStatus.Rejected, rejectedPR!.Status);

        // 5. Branch A1 user tries to query Company B -> Forbidden
        var forbiddenRes = await clientBranchA1.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-requisitions");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);
    }

    [Fact]
    public async Task CrossCompanyGM_CanAccessAssignedCompanies_CannotAccessForeignTenantCompany()
    {
        // MultiCompanyUserABId is assigned to Company A & Company B
        var clientMultiComp = _fixture.CreateAuthenticatedClient(
            TestConstants.MultiCompanyUserABId,
            "multicomp_user_ab",
            TestConstants.TenantAlphaId,
            new[] { "Branch User" },
            new[] { "PURCHASE_ORDER.VIEW", "PURCHASE_ORDER.CREATE" });

        // 1. Can view Company A, Branch A1
        var resCompA = await clientMultiComp.GetAsync(
            $"/api/companies/{TestConstants.CompanyAId}/branches/{TestConstants.BranchA1Id}/purchase-orders");
        Assert.Equal(HttpStatusCode.OK, resCompA.StatusCode);

        // 2. Can view Company B, Branch B1
        var resCompB = await clientMultiComp.GetAsync(
            $"/api/companies/{TestConstants.CompanyBId}/branches/{TestConstants.BranchB1Id}/purchase-orders");
        Assert.Equal(HttpStatusCode.OK, resCompB.StatusCode);

        // 3. Cannot access Company C (Tenant Beta) -> Forbidden / NotFound
        var resCompC = await clientMultiComp.GetAsync(
            $"/api/companies/{TestConstants.CompanyCId}/branches/{TestConstants.BranchC1Id}/purchase-orders");
        Assert.True(resCompC.StatusCode == HttpStatusCode.Forbidden || resCompC.StatusCode == HttpStatusCode.NotFound);
    }

    private class PurchaseOrderResponseDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public PurchaseOrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private class PurchaseRequisitionResponseDto
    {
        public Guid Id { get; set; }
        public string RequisitionNumber { get; set; } = string.Empty;
        public PurchaseRequisitionStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
    }

    private class ApprovalAuditRecordDto
    {
        public Guid Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public ApprovalDecision Decision { get; set; }
        public bool IsAutomated { get; set; }
        public string? Remarks { get; set; }
    }
}
