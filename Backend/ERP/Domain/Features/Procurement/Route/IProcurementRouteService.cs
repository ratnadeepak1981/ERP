using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Features.Procurement.RequestForQuotation;

namespace Domain.Features.Procurement.Route;

public interface IProcurementRouteService
{
    Task<ProcurementRouteStatusDto> GetRouteStatusAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CancellationToken ct = default);

    Task<RfqDetailsDto> CreateRfqFromRequisitionAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CreateRfqFromRequisitionRequest request,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> ResetCancelledRfqRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        ResetProcurementRouteRequest request,
        Guid userId,
        CancellationToken ct = default);

    Task ValidateDirectPoRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid requisitionId,
        CancellationToken ct = default);

    Task ValidateRfqAwardPoRouteAsync(
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        Guid rfqId,
        Guid quotationId,
        CancellationToken ct = default);
}
