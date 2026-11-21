using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Sales.SalesOrder;

namespace ERP.Application.Features.Sales.DirectOrder;

public interface IDirectCustomerOrderService
{
    Task<SalesOrderDto> CreateDirectB2BOrderAsync(
        Guid tenantId,
        Guid customerId,
        Guid customerContactId,
        CreateDirectB2BOrderRequest request,
        CancellationToken ct = default);
}
