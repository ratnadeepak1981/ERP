using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Sales.SalesOrder;

public interface ISalesOrderRepository
{
    Task<List<SalesOrder>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        SalesOrderStatus? status = null);

    Task<SalesOrder?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId);

    Task<bool> ExistsOrderNumberAsync(
        Guid tenantId,
        Guid companyId,
        string orderNumber);

    Task AddAsync(SalesOrder order);

    Task AddItemAsync(SalesOrderItem item);

    Task SaveChangesAsync();
}
