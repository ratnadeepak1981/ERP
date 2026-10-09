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
        CreatePurchaseOrderRequest request);

    Task<PurchaseOrderItem> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreatePurchaseOrderItemRequest request);
}
