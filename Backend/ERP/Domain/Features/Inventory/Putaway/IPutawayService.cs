using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Inventory.Putaway;

public interface IPutawayService
{
    Task<List<PutawayDto>> GetPutawaysAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId = null, PutawayStatus? status = null);
    Task<PutawayDto?> GetPutawayByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId);
    Task<List<EligibleSourceReceiptDto>> GetEligibleSourceReceiptsAsync(Guid tenantId, Guid companyId, Guid branchId, Guid? warehouseId = null);
    Task<List<EligibleSourceReceiptLineDto>> GetEligibleReceiptLinesAsync(Guid tenantId, Guid companyId, Guid branchId, PutawaySourceDocumentType docType, Guid sourceDocId);
    Task<PutawayDto> CreateDraftAsync(Guid tenantId, Guid companyId, Guid branchId, CreatePutawayDraftRequest request);
    Task<PutawayDto> UpdateDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId, UpdatePutawayDraftRequest request);
    Task<PutawayDto> ConfirmPutawayAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId);
    Task<PutawayDto> CancelDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid putawayId);
}
