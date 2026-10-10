using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.GoodsReceiptNote;

public interface IGoodsReceiptNoteService
{
    Task<List<GoodsReceiptNoteDto>> GetGrnsAsync(Guid tenantId, Guid companyId, Guid branchId);
    Task<GoodsReceiptNoteDto?> GetGrnByIdAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId);
    Task<GoodsReceiptNoteDto> CreateDraftFromPoAsync(Guid tenantId, Guid companyId, Guid branchId, Guid purchaseOrderId, string grnNumber, Guid? defaultWarehouseId = null, Guid? defaultLocationId = null, string? remarks = null);
    Task<GoodsReceiptNoteDto> CreateManualDraftAsync(Guid tenantId, Guid companyId, Guid branchId, CreateGoodsReceiptNoteRequest request);
    Task<GoodsReceiptNoteDto> UpdateDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId, UpdateGoodsReceiptNoteRequest request);
    Task<GoodsReceiptNoteDto> CancelDraftAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId);
    Task<GoodsReceiptNoteDto> ConfirmGrnAsync(Guid tenantId, Guid companyId, Guid branchId, Guid grnId);
}
