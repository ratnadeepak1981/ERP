using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.PurchaseRequisition;

public interface IPurchaseRequisitionRepository
{
    Task<List<PurchaseRequisition>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId);
    Task<PurchaseRequisition?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid requisitionId);
    Task<bool> ExistsRequisitionNumberAsync(Guid tenantId, Guid companyId, string requisitionNumber);
    Task AddAsync(PurchaseRequisition requisition);
    Task SaveChangesAsync();
}
