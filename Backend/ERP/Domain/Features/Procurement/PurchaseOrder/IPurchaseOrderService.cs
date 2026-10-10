using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.PurchaseOrder;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrder>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId);

    Task<PurchaseOrder?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId);

    Task<PurchaseOrder> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreatePurchaseOrderRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null);

    Task<PurchaseOrderItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreatePurchaseOrderItemRequest request);

    Task<PurchaseOrder> SubmitOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName);

    Task<PurchaseOrder> ApproveOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string? remarks = null);

    Task<PurchaseOrder> RejectOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid approverUserId,
        string approverUserName,
        string remarks);

    Task<PurchaseOrder> CancelOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);

    Task<List<Approval.ApprovalAuditRecord>> GetApprovalHistoryAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId);
}
