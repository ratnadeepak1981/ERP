using System;
using Domain.Features.Procurement.Approval;
using Domain.Features.Procurement.PurchaseOrder;
using Domain.Features.Procurement.PurchaseRequisition;
using Xunit;

namespace ERP.SecurityTests.Procurement;

public class ProcurementDomainUnitTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void PurchaseOrder_CreationAndAddItem_CalculatesAwayFromZeroRounding()
    {
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-001", DateTime.UtcNow, _supplierId, _userId);

        // Quantity: 3.33333, UnitPrice: 10.555 -> Rounded qty: 3.3333, Unit price: 10.56, LineTotal: 35.20
        po.AddItem(_productId, 3.33333m, 10.555m);

        Assert.Single(po.Items);
        var item = po.Items[0];
        Assert.Equal(3.3333m, item.Quantity);
        Assert.Equal(10.56m, item.UnitPrice);
        Assert.Equal(35.20m, item.LineTotal);
        Assert.Equal(35.20m, po.TotalAmount);
    }

    [Fact]
    public void PurchaseOrder_AddItemWithNegativeOrZeroValues_ThrowsArgumentException()
    {
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-002");

        Assert.Throws<ArgumentException>(() => po.AddItem(_productId, 0, 10m));
        Assert.Throws<ArgumentException>(() => po.AddItem(_productId, -5, 10m));
        Assert.Throws<ArgumentException>(() => po.AddItem(_productId, 5, -10m));
    }

    [Fact]
    public void PurchaseOrder_StateTransitions_EnforceGuards()
    {
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-003", DateTime.UtcNow, _supplierId, _userId);

        // Cannot submit empty order
        Assert.Throws<InvalidOperationException>(() => po.Submit());

        po.AddItem(_productId, 2, 50m);
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);

        // Submit succeeds
        po.Submit();
        Assert.Equal(PurchaseOrderStatus.Submitted, po.Status);

        // Cannot add item when submitted
        Assert.Throws<InvalidOperationException>(() => po.AddItem(_productId, 1, 20m));

        // Reject order
        po.Reject();
        Assert.Equal(PurchaseOrderStatus.Rejected, po.Status);

        // Cancel order from rejected
        po.Cancel();
        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);

        // Already cancelled cannot cancel again
        Assert.Throws<InvalidOperationException>(() => po.Cancel());
    }

    [Fact]
    public void PurchaseOrder_AutoApprove_TransitionsDirectlyToApproved()
    {
        var po = PurchaseOrder.Create(_tenantId, _companyId, _branchId, "PO-AUTO", DateTime.UtcNow, _supplierId, _userId);
        po.AddItem(_productId, 1, 100m);

        po.AutoApprove();
        Assert.Equal(PurchaseOrderStatus.Approved, po.Status);
    }

    [Fact]
    public void PurchaseRequisition_CreationAndItemRounding_CalculatesCorrectly()
    {
        var pr = PurchaseRequisition.Create(_tenantId, _companyId, _branchId, "PR-001", _userId, DateTime.UtcNow, null, "Urgent requirement");

        pr.AddItem(_productId, 5.5555m, 12.345m, remarks: "Raw material");

        Assert.Single(pr.Items);
        var item = pr.Items[0];
        Assert.Equal(5.5555m, item.Quantity);
        Assert.Equal(12.35m, item.EstimatedUnitPrice);
        Assert.Equal(68.61m, item.EstimatedLineTotal);
        Assert.Equal(68.61m, pr.TotalAmount);
    }

    [Fact]
    public void PurchaseRequisition_StateTransitions_EnforceGuards()
    {
        var pr = PurchaseRequisition.Create(_tenantId, _companyId, _branchId, "PR-002", _userId);

        // Cannot submit with no items
        Assert.Throws<InvalidOperationException>(() => pr.Submit());

        pr.AddItem(_productId, 10, 5m);
        pr.Submit();
        Assert.Equal(PurchaseRequisitionStatus.Submitted, pr.Status);

        // Cannot modify items once submitted
        Assert.Throws<InvalidOperationException>(() => pr.AddItem(_productId, 1, 5m));

        // Approve
        pr.Approve();
        Assert.Equal(PurchaseRequisitionStatus.Approved, pr.Status);

        // Cannot approve already approved
        Assert.Throws<InvalidOperationException>(() => pr.Approve());
    }

    [Fact]
    public async Task ApprovalWorkflowService_SelfApproval_IsProhibited()
    {
        var stubSettings = new StubSettingsProvider();
        var stubAudit = new StubAuditRepository();

        var workflowService = new ApprovalWorkflowService(stubSettings, stubAudit);

        var creatorId = Guid.NewGuid();
        var approverId = creatorId; // Attempting self-approval!

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflowService.RecordApprovalAsync(
                _tenantId,
                "PurchaseOrder",
                Guid.NewGuid(),
                approverId,
                "same_user",
                creatorId,
                100m);
        });

        Assert.Contains("own transactions", ex.Message);
    }

    [Fact]
    public async Task ApprovalWorkflowService_RejectionWithoutRemarks_ThrowsArgumentException()
    {
        var stubSettings = new StubSettingsProvider();
        var stubAudit = new StubAuditRepository();

        var workflowService = new ApprovalWorkflowService(stubSettings, stubAudit);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await workflowService.RecordRejectionAsync(
                _tenantId,
                "PurchaseOrder",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "approver",
                "",
                100m);
        });
    }

    private class StubSettingsProvider : IProcurementApprovalSettingsProvider
    {
        public Task<ProcurementApprovalSettings> GetSettingsAsync(Guid tenantId)
        {
            return Task.FromResult(new ProcurementApprovalSettings { ApprovalRequired = true, ApprovalMode = 1 });
        }
    }

    private class StubAuditRepository : IApprovalAuditRepository
    {
        public Task AddRecordAsync(ApprovalAuditRecord record) => Task.CompletedTask;
        public Task<System.Collections.Generic.List<ApprovalAuditRecord>> GetRecordsAsync(Guid tenantId, string documentType, Guid documentId)
            => Task.FromResult(new System.Collections.Generic.List<ApprovalAuditRecord>());
        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
