using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Features.Procurement.Approval;

namespace Domain.Features.Procurement.PurchaseRequisition;

public interface IPurchaseRequisitionService
{
    Task<List<PurchaseRequisition>> GetRequisitionsAsync(Guid tenantId, Guid companyId, Guid branchId);

    Task<PurchaseRequisition?> GetRequisitionAsync(Guid tenantId, Guid companyId, Guid branchId, Guid requisitionId);

    Task<PurchaseRequisition> CreateRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePurchaseRequisitionRequest request,
        Guid requestorUserId,
        string requestorUserName);

    Task<PurchaseRequisitionItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CreatePurchaseRequisitionItemRequest request);

    Task<PurchaseRequisition> SubmitRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid actorUserId,
        string actorUserName);

    Task<PurchaseRequisition> ApproveRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null);

    Task<PurchaseRequisition> RejectRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid approverUserId,
        string approverUserName,
        string remarks);

    Task<PurchaseRequisition> CancelRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);

    Task<List<ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId);
}
