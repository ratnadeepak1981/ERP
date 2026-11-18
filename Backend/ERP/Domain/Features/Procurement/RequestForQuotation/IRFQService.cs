using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Features.Procurement.RequestForQuotation;

public interface IRFQService
{
    Task<RfqDetailsDto> CreateRfqAsync(CreateRfqRequest request, Guid userId, Guid tenantId, Guid companyId, Guid branchId, CancellationToken ct = default);
    Task<RfqDetailsDto> UpdateRfqAsync(Guid id, UpdateRfqRequest request, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto?> GetRfqByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<RfqHeaderDto>> GetRfqsAsync(Guid tenantId, Guid companyId, Guid? branchId = null, RfqFilterRequest? filter = null, CancellationToken ct = default);
    Task<RfqDetailsDto> AddLineAsync(Guid rfqId, CreateRfqLineRequest request, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto> RemoveLineAsync(Guid rfqId, Guid lineId, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto> AddSupplierAsync(Guid rfqId, AddRfqSupplierRequest request, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto> RemoveSupplierAsync(Guid rfqId, Guid supplierId, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqInvitationResultDto> SendInvitationsAsync(Guid rfqId, SendRfqInvitationsRequest request, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto> CloseRfqAsync(Guid rfqId, Guid userId, Guid tenantId, CancellationToken ct = default);
    Task<RfqDetailsDto> CancelRfqAsync(Guid rfqId, string? reason, Guid userId, Guid tenantId, CancellationToken ct = default);
}
