using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public interface IGoodsReceiptNoteRepository
{
    Task<List<GoodsReceiptNote>> GetByBranchAsync(Guid tenantId, Guid companyId, Guid branchId);
    Task<GoodsReceiptNote?> GetByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId);
    Task<GoodsReceiptNote?> GetByIdWithLinesAsync(Guid tenantId, Guid grnId);
    Task<bool> ExistsGrnNumberAsync(Guid tenantId, Guid companyId, string grnNumber);
    Task<List<GoodsReceiptNote>> GetByPurchaseOrderIdAsync(Guid tenantId, Guid purchaseOrderId);
    Task AddAsync(GoodsReceiptNote grn);
    Task DeleteDraftAsync(GoodsReceiptNote grn);
    Task SaveChangesAsync();
}
