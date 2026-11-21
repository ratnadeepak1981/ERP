using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Sales.SalesOrder;

namespace ERP.Application.Features.Sales.CustomerPortal;

public interface ICustomerOrderPortalService
{
    Task<List<CustomerOrderSummaryDto>> GetOrdersAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        SalesOrderStatus? status = null,
        CancellationToken ct = default);

    Task<CustomerOrderDetailDto> GetOrderByIdAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct = default);

    Task<CustomerOrderTrackingDto> GetOrderTrackingAsync(
        Guid tenantId,
        Guid customerId,
        Guid orderId,
        CancellationToken ct = default);

    Task<List<CustomerInvoiceSummaryDto>> GetInvoicesAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        CancellationToken ct = default);

    Task<CustomerDashboardDto> GetDashboardAsync(
        Guid tenantId,
        Guid customerId,
        Guid? sellingCompanyId = null,
        CancellationToken ct = default);
}
