using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.PurchaseOrder;

public interface IPurchaseOrderRepository
{
    Task<List<PurchaseOrder>> GetByBranchAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId);

    Task<List<PurchaseOrder>> GetByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        IEnumerable<Guid>? branchIds = null);

    Task<PurchaseOrder?> GetByIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid orderId);

    Task<PurchaseOrder?> GetByIdAndCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid orderId);

    Task<bool> ExistsOrderNumberAsync(
        Guid tenantId,
        Guid companyId,
        string orderNumber);

    Task AddAsync(PurchaseOrder order);

    Task SaveChangesAsync();
}
