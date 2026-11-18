using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.RequestForQuotation;

public interface ISupplierQuotationService
{
    Task<PublicRfqDetailsDto?> GetPublicRfqDetailsByTokenAsync(string token, CancellationToken ct = default);
    Task<SubmitQuotationResultDto> SubmitPublicQuotationAsync(string token, SubmitPublicQuotationRequest request, CancellationToken ct = default);
    Task<SupplierQuotationDto> RecordManualQuotationAsync(Guid rfqId, RecordManualQuotationRequest request, Guid userId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierQuotationDto>> GetQuotationsForRfqAsync(Guid rfqId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<SupplierQuotationDto?> GetQuotationByIdAsync(Guid rfqId, Guid quotationId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<RfqComparisonMatrixDto> GetComparisonMatrixAsync(Guid rfqId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<SupplierQuotationDto> ApproveQuotationAsync(Guid rfqId, Guid quotationId, ApproveQuotationRequest request, Guid userId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<SupplierQuotationDto> RejectQuotationAsync(Guid rfqId, Guid quotationId, RejectQuotationRequest request, Guid userId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
}
