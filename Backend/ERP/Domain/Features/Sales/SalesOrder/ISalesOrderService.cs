using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.SalesOrder;

public interface ISalesOrderService
{
    Task<List<SalesOrderDto>> GetOrdersAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesOrderStatus? status = null);

    Task<SalesOrderDto?> GetOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId);

    Task<SalesOrderDto> CreateOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        CreateSalesOrderRequest request,
        Guid? actorUserId = null,
        string? actorUserName = null);

    Task<SalesOrderItemDto> AddItemAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        CreateSalesOrderItemRequest request);

    Task<SalesOrderDto> SubmitOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName);

    Task<SalesOrderDto> ConfirmOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);

    Task<SalesOrderDto> RejectOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string remarks);

    Task<SalesOrderDto> CancelOrderAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId,
        Guid actorUserId,
        string actorUserName,
        string? remarks = null);
}
